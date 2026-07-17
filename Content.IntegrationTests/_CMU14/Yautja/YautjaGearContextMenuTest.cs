using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.ContextMenu.UI;
using Content.Client.Gameplay;
using Content.Client.Inventory;
using Content.Client.Verbs.UI;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Blocking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Robust.Client.UserInterface;
using Robust.Client.State;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaGearContextMenuTest
{
#nullable enable
    private sealed record GearScenario(
        string Name,
        YautjaGearKind Kind,
        string? AttachmentPrototype,
        string ActionPrototype,
        Func<InstantActionEvent> CreateEvent,
        bool StartBlocking = false);

    private sealed record BracerRmbScenario(
        string Name,
        string? AttachmentPrototype,
        bool StartBlocking = false);
#nullable restore

    private static readonly GearScenario BlockingShieldGearScenario = new(
        "attachment shield while blocking",
        YautjaGearKind.Shield,
        "CMUYautjaBracerShieldAttachment",
        "CMUActionYautjaToggleShield",
        () => new YautjaToggleShieldActionEvent(),
        StartBlocking: true);

    private static readonly GearScenario[] Scenarios =
    [
        new("direct caster", YautjaGearKind.Caster, null, "CMUActionYautjaToggleCaster", () => new YautjaToggleCasterActionEvent()),
        new("direct scimitar", YautjaGearKind.Scimitar, null, "CMUActionYautjaToggleScimitar", () => new YautjaToggleScimitarActionEvent()),
        new("direct shield", YautjaGearKind.Shield, null, "CMUActionYautjaToggleShield", () => new YautjaToggleShieldActionEvent()),
        new("direct chain gauntlet", YautjaGearKind.ChainGauntlet, null, "CMUActionYautjaToggleChainGauntlet", () => new YautjaToggleChainGauntletActionEvent()),
        new("attachment wrist blades", YautjaGearKind.WristBlades, "CMUYautjaWristBladesAttachment", "CMUActionYautjaToggleWristBlades", () => new YautjaToggleWristBladesActionEvent()),
        new("attachment scimitar", YautjaGearKind.Scimitar, "CMUYautjaScimitarAttachment", "CMUActionYautjaToggleScimitar", () => new YautjaToggleScimitarActionEvent()),
        new("attachment alternate scimitar", YautjaGearKind.Scimitar, "CMUYautjaScimitarAltAttachment", "CMUActionYautjaToggleScimitar", () => new YautjaToggleScimitarActionEvent()),
        BlockingShieldGearScenario,
        new("attachment chain gauntlet", YautjaGearKind.ChainGauntlet, "CMUYautjaChainGauntletsAttachment", "CMUActionYautjaToggleChainGauntlet", () => new YautjaToggleChainGauntletActionEvent()),
    ];

    private static readonly BracerRmbScenario[] BracerRmbScenarios =
    [
        new("default internal gear", null),
        new("wrist blades attachment", "CMUYautjaWristBladesAttachment"),
        new("scimitar attachment", "CMUYautjaScimitarAttachment"),
        new("alternate scimitar attachment", "CMUYautjaScimitarAltAttachment"),
        new("shield attachment", "CMUYautjaBracerShieldAttachment"),
        new("shield attachment deployed while blocking", "CMUYautjaBracerShieldAttachment", StartBlocking: true),
        new("chain gauntlet attachment", "CMUYautjaChainGauntletsAttachment"),
    ];

    [Test]
    public async Task AllDeployedBracerGearContextMenusCanRender()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid? previousAttached = null;

        var state = client.ResolveDependency<IStateManager>();
        await client.WaitPost(() => state.RequestStateChange<GameplayState>());
        await pair.ReallyBeIdle(5);

        await server.WaitAssertion(() =>
        {
            previousAttached = server.PlayerMan.Sessions.Single().AttachedEntity;
        });

        try
        {
            foreach (var scenario in Scenarios)
            {
                EntityUid hunter = default;
                EntityUid bracer = default;
                EntityUid source = default;
                EntityUid deployed = default;
                EntityUid action = default;
                EntityUid attachment = default;
                List<Verb> serverVerbs = new();
                List<Verb> responseVerbs = new();
                var gotResponse = false;

                try
                {
                    await server.WaitAssertion(() =>
                    {
                        var entMan = server.EntMan;
                        var hands = entMan.System<SharedHandsSystem>();
                        var inventory = entMan.System<InventorySystem>();
                        var session = server.PlayerMan.Sessions.Single();

                        hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                        bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                        action = entMan.SpawnEntity(scenario.ActionPrototype, MapCoordinates.Nullspace);
                        server.PlayerMan.SetAttachedEntity(session, hunter);

                        entMan.EnsureComponent<YautjaComponent>(hunter);
                        Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true),
                            Is.True,
                            scenario.Name);
                        entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 1000;

                        var gear = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                        if (scenario.AttachmentPrototype == null)
                        {
                            source = gear.Gear[scenario.Kind];
                        }
                        else
                        {
                            attachment = entMan.SpawnEntity(scenario.AttachmentPrototype, map.GridCoords);
                            Assert.That(hands.TryPickupAnyHand(hunter, attachment), Is.True, scenario.Name);

                            var install = new InteractUsingEvent(
                                hunter,
                                attachment,
                                bracer,
                                entMan.GetComponent<TransformComponent>(bracer).Coordinates);
                            entMan.EventBus.RaiseLocalEvent(bracer, install);
                            Assert.That(install.Handled, Is.True, scenario.Name);
                            RaiseDialogOption(entMan, bracer, hunter, "Left");
                            source = attachment;
                        }

                        RaiseGearAction(entMan, bracer, hunter, action, scenario);

                        var stored = entMan.GetComponent<YautjaStoredGearComponent>(source);
                        deployed = stored.AttachedWeapon ?? source;
                        Assert.Multiple(() =>
                        {
                            Assert.That(stored.Deployed, Is.True, scenario.Name);
                            Assert.That(hands.IsHolding(hunter, deployed), Is.True, scenario.Name);
                        });

                        if (scenario.StartBlocking)
                        {
                            var blocking = entMan.System<BlockingSystem>();
                            var blockingComponent = entMan.GetComponent<BlockingComponent>(deployed);
                            Assert.That(blocking.StartBlocking(deployed, blockingComponent, hunter), Is.True, scenario.Name);
                        }

                        serverVerbs = entMan.System<Content.Server.Verbs.VerbSystem>()
                            .GetLocalVerbs(deployed, hunter, Verb.VerbTypes, force: false)
                            .ToList();
                    });

                    await pair.RunTicksSync(5);

                    await client.WaitPost(() =>
                    {
                        var entMan = client.EntMan;
                        var clientDeployed = entMan.GetEntity(server.EntMan.GetNetEntity(deployed));
                        var clientVerbs = entMan.System<Content.Client.Verbs.VerbSystem>();
                        var netDeployed = entMan.GetNetEntity(clientDeployed);
                        var ui = client.ResolveDependency<IUserInterfaceManager>();
                        var context = ui.GetUIController<ContextMenuUIController>();

                        Assert.DoesNotThrow(
                            () => ui.GetUIController<EntityMenuUIController>().OpenRootMenu([clientDeployed]),
                            $"{scenario.Name}: entity menu");
                        context.Close();

                        void Handler(VerbsResponseEvent response)
                        {
                            if (response.Entity != netDeployed)
                                return;

                            responseVerbs = response.Verbs;
                            gotResponse = true;
                            clientVerbs.OnVerbsResponse -= Handler;
                        }

                        clientVerbs.OnVerbsResponse += Handler;
                        Assert.DoesNotThrow(
                            () => ui.GetUIController<VerbMenuUIController>().OpenVerbMenu(clientDeployed, force: false),
                            $"{scenario.Name}: verb menu");
                    });

                    await pair.RunTicksSync(5);

                    await client.WaitAssertion(() =>
                    {
                        var entMan = client.EntMan;
                        var player = client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalEntity;
                        Assert.That(player, Is.Not.Null, scenario.Name);
                        Assert.That(gotResponse, Is.True, scenario.Name);

                        var clientDeployed = entMan.GetEntity(server.EntMan.GetNetEntity(deployed));
                        var localVerbs = entMan.System<Content.Client.Verbs.VerbSystem>()
                            .GetLocalVerbs(clientDeployed, player!.Value, Verb.VerbTypes, force: false);
                        var verbMenu = client.ResolveDependency<IUserInterfaceManager>()
                            .GetUIController<VerbMenuUIController>();

                        Assert.That(verbMenu.CurrentVerbs, Is.Not.Empty, scenario.Name);
                        foreach (var verb in localVerbs
                                     .Concat(serverVerbs)
                                     .Concat(responseVerbs)
                                     .Concat(verbMenu.CurrentVerbs))
                        {
                            AssertVerbMenuElementCanRender(verb, scenario.Name);
                        }

                        client.ResolveDependency<IUserInterfaceManager>()
                            .GetUIController<ContextMenuUIController>()
                            .Close();
                    });

                    await server.WaitAssertion(() =>
                    {
                        var entMan = server.EntMan;
                        var hands = entMan.System<SharedHandsSystem>();
                        var stored = entMan.GetComponent<YautjaStoredGearComponent>(source);
                        if (scenario.AttachmentPrototype == null)
                        {
                            RaiseGearAction(entMan, bracer, hunter, action, scenario);

                            Assert.Multiple(() =>
                            {
                                Assert.That(stored.Deployed, Is.False, scenario.Name);
                                Assert.That(hands.IsHolding(hunter, deployed), Is.False, scenario.Name);

                                if (entMan.TryGetComponent(deployed, out BlockingComponent blocking))
                                    Assert.That(blocking.IsBlocking, Is.False, scenario.Name);
                            });
                        }
                    });
                }
                finally
                {
                    await server.WaitAssertion(() =>
                    {
                        var entMan = server.EntMan;
                        server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), previousAttached);

                        foreach (var uid in new[] { hunter, bracer, source, deployed, action, attachment }.Distinct())
                        {
                            if (uid != default && !entMan.Deleted(uid))
                                entMan.DeleteEntity(uid);
                        }
                    });
                }

                await pair.RunTicksSync(2);
            }
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var session = server.PlayerMan.Sessions.SingleOrDefault();
                if (session != null)
                    server.PlayerMan.SetAttachedEntity(session, previousAttached);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllBracerContextMenuEntryPointsCanRender()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid? previousAttached = null;

        var state = client.ResolveDependency<IStateManager>();
        await client.WaitPost(() => state.RequestStateChange<GameplayState>());
        await pair.ReallyBeIdle(5);

        await server.WaitAssertion(() =>
        {
            previousAttached = server.PlayerMan.Sessions.Single().AttachedEntity;
        });

        try
        {
            foreach (var scenario in BracerRmbScenarios)
            {
                foreach (var world in scenario.StartBlocking ? new[] { false } : new[] { false, true })
                {
                    EntityUid hunter = default;
                    EntityUid bracer = default;
                    EntityUid attachment = default;
                    EntityUid action = default;
                    EntityUid deployed = default;

                    try
                    {
                        await server.WaitAssertion(() =>
                        {
                            var entMan = server.EntMan;
                            var hands = entMan.System<SharedHandsSystem>();
                            var inventory = entMan.System<InventorySystem>();
                            var session = server.PlayerMan.Sessions.Single();

                            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                            bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                            server.PlayerMan.SetAttachedEntity(session, hunter);
                            entMan.EnsureComponent<YautjaComponent>(hunter);

                            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true),
                                Is.True,
                                scenario.Name);
                            entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 1000;

                            if (scenario.AttachmentPrototype != null)
                            {
                                attachment = entMan.SpawnEntity(scenario.AttachmentPrototype, map.GridCoords);
                                Assert.That(hands.TryPickupAnyHand(hunter, attachment), Is.True, scenario.Name);

                                var install = new InteractUsingEvent(
                                    hunter,
                                    attachment,
                                    bracer,
                                    entMan.GetComponent<TransformComponent>(bracer).Coordinates);
                                entMan.EventBus.RaiseLocalEvent(bracer, install);
                                Assert.That(install.Handled, Is.True, scenario.Name);
                                RaiseDialogOption(entMan, bracer, hunter, "Left");
                            }

                            if (scenario.StartBlocking)
                            {
                                Assert.That(attachment, Is.Not.EqualTo(default(EntityUid)), scenario.Name);
                                action = entMan.SpawnEntity(BlockingShieldGearScenario.ActionPrototype, MapCoordinates.Nullspace);
                                RaiseGearAction(entMan, bracer, hunter, action, BlockingShieldGearScenario);

                                var stored = entMan.GetComponent<YautjaStoredGearComponent>(attachment);
                                deployed = stored.AttachedWeapon ?? attachment;
                                Assert.Multiple(() =>
                                {
                                    Assert.That(stored.Deployed, Is.True, scenario.Name);
                                    Assert.That(hands.IsHolding(hunter, deployed), Is.True, scenario.Name);
                                });

                                var blocking = entMan.System<BlockingSystem>();
                                var blockingComponent = entMan.GetComponent<BlockingComponent>(deployed);
                                Assert.That(blocking.StartBlocking(deployed, blockingComponent, hunter), Is.True, scenario.Name);
                                Assert.That(blockingComponent.IsBlocking, Is.True, scenario.Name);
                            }

                            if (world)
                            {
                                Assert.That(inventory.TryUnequip(hunter, "gloves", out var removed,
                                    silent: true, force: true), Is.True, scenario.Name);
                                Assert.That(removed, Is.EqualTo(bracer), scenario.Name);
                            }
                        });

                        await pair.RunTicksSync(5);

                        if (!world)
                        {
                            // Exercise the production race boundary: close the equipped-item menu
                            // before the server has a chance to answer, then ensure that late answer
                            // cannot revive or mutate the closed popup.
                            var closedResponseReceived = false;
                            await client.WaitPost(() =>
                            {
                                var entMan = client.EntMan;
                                var clientBracer = entMan.GetEntity(server.EntMan.GetNetEntity(bracer));
                                var localPlayer = client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalEntity;
                                var ui = client.ResolveDependency<IUserInterfaceManager>();
                                var context = ui.GetUIController<ContextMenuUIController>();
                                var verbs = ui.GetUIController<VerbMenuUIController>();
                                var clientVerbs = entMan.System<Content.Client.Verbs.VerbSystem>();
                                var netBracer = entMan.GetNetEntity(clientBracer);

                                void Handler(VerbsResponseEvent response)
                                {
                                    if (response.Entity != netBracer)
                                        return;

                                    closedResponseReceived = true;
                                    clientVerbs.OnVerbsResponse -= Handler;
                                }

                                clientVerbs.OnVerbsResponse += Handler;

                                Assert.That(localPlayer, Is.Not.Null, scenario.Name);
                                Assert.DoesNotThrow(
                                    () => entMan.System<ClientInventorySystem>()
                                        .UIInventoryOpenContextMenu("gloves", localPlayer!.Value),
                                    $"{scenario.Name}: close-before-response open");
                                Assert.That(verbs.OpenMenu, Is.Not.Null, scenario.Name);

                                Assert.DoesNotThrow(context.Close, $"{scenario.Name}: close before response");
                                Assert.That(verbs.OpenMenu, Is.Null, scenario.Name);
                            });

                            await pair.RunTicksSync(5);
                            await client.WaitAssertion(() =>
                            {
                                var verbs = client.ResolveDependency<IUserInterfaceManager>()
                                    .GetUIController<VerbMenuUIController>();
                                Assert.Multiple(() =>
                                {
                                    Assert.That(closedResponseReceived, Is.True, scenario.Name);
                                    Assert.That(verbs.OpenMenu, Is.Null, scenario.Name);
                                });
                            });
                        }

                        for (var attempt = 1; attempt <= 2; attempt++)
                        {
                            var responseReceived = false;
                            List<Verb> responseVerbs = new();
                            await client.WaitPost(() =>
                            {
                                var entMan = client.EntMan;
                                var clientBracer = entMan.GetEntity(server.EntMan.GetNetEntity(bracer));
                                var localPlayer = client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalEntity;
                                Assert.That(localPlayer, Is.Not.Null, scenario.Name);

                                var ui = client.ResolveDependency<IUserInterfaceManager>();
                                var context = ui.GetUIController<ContextMenuUIController>();
                                var verbs = ui.GetUIController<VerbMenuUIController>();
                                var clientVerbs = entMan.System<Content.Client.Verbs.VerbSystem>();
                                var netBracer = entMan.GetNetEntity(clientBracer);

                                void Handler(VerbsResponseEvent response)
                                {
                                    if (response.Entity != netBracer)
                                        return;

                                    responseVerbs = response.Verbs;
                                    responseReceived = true;
                                    clientVerbs.OnVerbsResponse -= Handler;
                                }

                                clientVerbs.OnVerbsResponse += Handler;

                                if (world)
                                {
                                    var entityMenu = ui.GetUIController<EntityMenuUIController>();
                                    entityMenu.OpenRootMenu([clientBracer]);
                                    Assert.That(entityMenu.Elements.TryGetValue(clientBracer, out var element), Is.True, scenario.Name);
                                    Assert.DoesNotThrow(() => context.OpenSubMenu(element!), scenario.Name);
                                }
                                else
                                {
                                    var inventory = entMan.System<ClientInventorySystem>();
                                    Assert.DoesNotThrow(
                                        () => inventory.UIInventoryOpenContextMenu("gloves", localPlayer!.Value),
                                        scenario.Name);
                                }

                                Assert.That(verbs.CurrentTarget, Is.EqualTo(netBracer), scenario.Name);
                            });

                            await pair.RunTicksSync(5);

                            await client.WaitAssertion(() =>
                            {
                                var entMan = client.EntMan;
                                var clientBracer = entMan.GetEntity(server.EntMan.GetNetEntity(bracer));
                                var ui = client.ResolveDependency<IUserInterfaceManager>();
                                var verbs = ui.GetUIController<VerbMenuUIController>();
                                var openMenu = verbs.OpenMenu;

                                Assert.Multiple(() =>
                                {
                                    Assert.That(responseReceived, Is.True, scenario.Name);
                                    Assert.That(openMenu, Is.Not.Null, scenario.Name);
                                    Assert.That(openMenu!.Disposed, Is.False, scenario.Name);
                                    Assert.That(openMenu!.MenuBody.Disposed, Is.False, scenario.Name);
                                    Assert.That(openMenu!.Visible, Is.True, scenario.Name);
                                    Assert.That(verbs.CurrentTarget, Is.EqualTo(entMan.GetNetEntity(clientBracer)), scenario.Name);

                                    foreach (var responseVerb in responseVerbs)
                                        Assert.That(verbs.CurrentVerbs.Contains(responseVerb), Is.True, scenario.Name);

                                    foreach (var verb in verbs.CurrentVerbs)
                                        AssertVerbMenuElementCanRender(verb, scenario.Name);
                                });
                            });

                            await client.WaitPost(() =>
                            {
                                var ui = client.ResolveDependency<IUserInterfaceManager>();
                                Assert.DoesNotThrow(
                                    () => ui.GetUIController<ContextMenuUIController>().Close(),
                                    $"{scenario.Name} ({(world ? "world" : "worn")}), attempt {attempt}: close");
                                Assert.That(ui.GetUIController<VerbMenuUIController>().OpenMenu, Is.Null, scenario.Name);
                                Assert.That(ui.GetUIController<EntityMenuUIController>().Elements, Is.Empty, scenario.Name);
                            });
                        }
                    }
                    finally
                    {
                        await server.WaitAssertion(() =>
                        {
                            var entMan = server.EntMan;
                            var session = server.PlayerMan.Sessions.SingleOrDefault();
                            if (session != null)
                                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                            foreach (var uid in new[] { hunter, bracer, attachment, action, deployed }.Distinct())
                            {
                                if (uid != default && !entMan.Deleted(uid))
                                    entMan.DeleteEntity(uid);
                            }
                        });
                    }

                    await pair.RunTicksSync(2);
                }
            }
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var session = server.PlayerMan.Sessions.SingleOrDefault();
                if (session != null)
                    server.PlayerMan.SetAttachedEntity(session, previousAttached);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static void RaiseGearAction(
        IEntityManager entMan,
        EntityUid bracer,
        EntityUid hunter,
        EntityUid action,
        GearScenario scenario)
    {
        var ev = scenario.CreateEvent();
        ev.Performer = hunter;
        ev.Action = (action, entMan.GetComponent<ActionComponent>(action));

        switch (ev)
        {
            case YautjaToggleCasterActionEvent caster:
                entMan.EventBus.RaiseLocalEvent(bracer, caster);
                break;
            case YautjaToggleWristBladesActionEvent wristBlades:
                entMan.EventBus.RaiseLocalEvent(bracer, wristBlades);
                break;
            case YautjaToggleScimitarActionEvent scimitar:
                entMan.EventBus.RaiseLocalEvent(bracer, scimitar);
                break;
            case YautjaToggleShieldActionEvent shield:
                entMan.EventBus.RaiseLocalEvent(bracer, shield);
                break;
            case YautjaToggleChainGauntletActionEvent chainGauntlet:
                entMan.EventBus.RaiseLocalEvent(bracer, chainGauntlet);
                break;
            default:
                Assert.Fail($"Unsupported gear action event {ev.GetType().Name}");
                break;
        }

        Assert.That(ev.Handled, Is.True, scenario.Name);
    }

    private static void RaiseDialogOption(
        IEntityManager entMan,
        EntityUid bracer,
        EntityUid user,
        string optionText)
    {
        var dialog = entMan.GetComponent<DialogComponent>(bracer);
        var optionIndex = dialog.Options
            .Select((option, index) => (option, index))
            .Single(pair => pair.option.Text == optionText)
            .index;

        entMan.EventBus.RaiseLocalEvent(bracer, new DialogOptionBuiMsg(optionIndex)
        {
            Actor = user,
            UiKey = DialogUiKey.Key,
        });
    }

    private static void AssertVerbMenuElementCanRender(Verb verb, string scenario)
    {
        Assert.DoesNotThrow(() =>
        {
            var element = new VerbMenuElement(verb);
            _ = element.TooltipSupplier?.Invoke(element);
        }, $"{scenario}: verb {verb.Text}");
    }
}
