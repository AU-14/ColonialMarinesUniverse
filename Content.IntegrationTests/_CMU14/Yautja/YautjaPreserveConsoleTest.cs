using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Popups;
using Content.Server.Administration.Logs;
using Content.Server.Power.Components;
using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Power;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaPreserveConsoleTest
{
    [Test]
    public async Task HuntingGroundEscapePrototypesMatchCmss13PreserveContract()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var shutter = prototypes.Index<EntityPrototype>("CMUYautjaHuntingGroundPreserveShutter");
            var console = prototypes.Index<EntityPrototype>("CMUYautjaHuntingGroundEscapeConsole");
            var edge = prototypes.Index<EntityPrototype>("CMUYautjaHuntingGroundPreserveEdge");

            Assert.Multiple(() =>
            {
                Assert.That(shutter.TryGetComponent<YautjaPreserveShutterComponent>(out _, factory), Is.True);
                Assert.That(shutter.TryGetComponent<DoorComponent>(out var door, factory), Is.True);
                Assert.That(door!.State, Is.EqualTo(DoorState.Closed));
                Assert.That(door.CanPry, Is.False);
                Assert.That(shutter.TryGetComponent<ApcPowerReceiverComponent>(out var apc, factory), Is.True);
                Assert.That(apc!.NeedsPower, Is.False);
                Assert.That(shutter.TryGetComponent<RMCPowerReceiverComponent>(out var rmcPower, factory), Is.True);
                Assert.That(rmcPower!.IdleLoad, Is.Zero);
                Assert.That(rmcPower.ActiveLoad, Is.Zero);
                Assert.That(console.TryGetComponent<YautjaHuntEscapeConsoleComponent>(out _, factory), Is.True);
                Assert.That(console.TryGetComponent<FixturesComponent>(out var fixtures, factory), Is.True);
                Assert.That(fixtures!.Fixtures, Is.Not.Empty, "The console must be a collidable structure, like the CMSS13 escape console.");
                Assert.That(edge.TryGetComponent<YautjaPreserveEdgeComponent>(out _, factory), Is.True);
                Assert.That(edge.TryGetComponent<FixturesComponent>(out var edgeFixtures, factory), Is.True);
                Assert.That(edgeFixtures!.Fixtures, Is.Not.Empty, "The preserve edge must block movement until the escape completes.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEdgeRejectsYautjaAndMovesPreyOutAfterFiveSeconds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid hunterEdge = default;
        EntityUid preyEdge = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                hunterEdge = entMan.SpawnEntity("CMUYautjaHuntingGroundPreserveEdge", map.GridCoords.Offset(new Vector2(1, 0)));
                preyEdge = entMan.SpawnEntity("CMUYautjaHuntingGroundPreserveEdge", map.GridCoords.Offset(new Vector2(3, 0)));
                entMan.EnsureComponent<YautjaComponent>(hunter);

                entMan.EventBus.RaiseLocalEvent(hunterEdge, new InteractHandEvent(hunter, hunterEdge));
                Assert.That(entMan.TryGetComponent(hunterEdge, out DialogComponent? _), Is.False);

                entMan.EventBus.RaiseLocalEvent(preyEdge, new InteractHandEvent(prey, preyEdge));
                Assert.That(entMan.TryGetComponent(preyEdge, out DialogComponent? _), Is.True);

                entMan.EventBus.RaiseLocalEvent(preyEdge, new DialogOptionBuiMsg(0)
                {
                    Actor = prey,
                    UiKey = DialogUiKey.Key,
                });

                Assert.That(entMan.GetComponent<DoAfterComponent>(prey).DoAfters.Values,
                    Has.Some.Matches<DoAfter>(active =>
                        !active.Cancelled &&
                        !active.Completed &&
                        active.Args.Event is YautjaPreserveEscapeDoAfterEvent));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(5.5f));

            await server.WaitAssertion(() =>
                Assert.That(server.EntMan.GetComponent<TransformComponent>(prey).MapID, Is.EqualTo(MapId.Nullspace)));
        }
        finally
        {
            await server.WaitPost(() => DeleteAll(server.EntMan, hunter, prey, hunterEdge, preyEdge));
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleYautjaDialogUsesCmss13TguiAlertText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                entMan.System<MetaDataSystem>().SetEntityName(console, "preserve shutter console");

                entMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));

                Assert.That(entMan.TryGetComponent(console, out DialogComponent? dialog), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(dialog!.Title, Is.EqualTo("preserve shutter console"));
                    Assert.That(dialog.Message.Text, Is.EqualTo("Do you wish to close or open the shutter?"));
                    Assert.That(dialog.Options.Select(option => option.Text), Is.EquivalentTo(new[] { "Open", "Close" }));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                DeleteAll(entMan, hunter, console);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleYautjaDialogOptionsControlShutter()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;
        EntityUid shutter = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);
                shutter = entMan.SpawnEntity("CMUHunterShipObjStructureMachineryDoorPoddoorHybrisaOpenShuttersAlmayerPdoor1EastId1", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                entMan.EnsureComponent<YautjaPreserveShutterComponent>(shutter);

                entMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));
                Assert.That(entMan.TryGetComponent(console, out DialogComponent? _), Is.True);

                entMan.EventBus.RaiseLocalEvent(console, new DialogOptionBuiMsg(0)
                {
                    Actor = hunter,
                    UiKey = DialogUiKey.Key,
                });

                Assert.That(entMan.GetComponent<YautjaHuntEscapeConsoleComponent>(console).Opened, Is.True);
                Assert.That(entMan.GetComponent<DoorComponent>(shutter).State, Is.Not.EqualTo(DoorState.Closed));

                entMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));
                entMan.EventBus.RaiseLocalEvent(console, new DialogOptionBuiMsg(1)
                {
                    Actor = hunter,
                    UiKey = DialogUiKey.Key,
                });

                Assert.That(entMan.GetComponent<YautjaHuntEscapeConsoleComponent>(console).Opened, Is.False);
                Assert.That(entMan.GetComponent<DoorComponent>(shutter).State, Is.Not.EqualTo(DoorState.Open));
            });

        }
        finally
        {
            await server.WaitPost(() => DeleteAll(server.EntMan, hunter, console, shutter));
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleYautjaDialogTimesOutWithoutOpeningLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;
        EntityUid shutter = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);
                shutter = entMan.SpawnEntity("CMUHunterShipObjStructureMachineryDoorPoddoorHybrisaOpenShuttersAlmayerPdoor1EastId1", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                entMan.EnsureComponent<YautjaPreserveShutterComponent>(shutter);

                entMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));

                Assert.That(entMan.TryGetComponent(console, out DialogComponent? _), Is.True);
                Assert.That(entMan.GetComponent<YautjaHuntEscapeConsoleComponent>(console).Opened, Is.False);
                Assert.That(entMan.GetComponent<DoorComponent>(shutter).State, Is.EqualTo(DoorState.Closed));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(14.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.TryGetComponent(console, out DialogComponent? _), Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.1f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.TryGetComponent(console, out DialogComponent? _), Is.False);
                    Assert.That(entMan.GetComponent<YautjaHuntEscapeConsoleComponent>(console).Opened, Is.False);
                    Assert.That(entMan.GetComponent<DoorComponent>(shutter).State, Is.EqualTo(DoorState.Closed));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                DeleteAll(entMan, hunter, console, shutter);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleNonYautjaHandPromptUsesCmss13ScanMaskText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid console = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);

                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                server.PlayerMan.SetAttachedEntity(session, user);

                var interact = new InteractHandEvent(user, console);
                entMan.EventBus.RaiseLocalEvent(console, interact);

                Assert.That(interact.Handled, Is.True);
                Assert.That(entMan.TryGetComponent(console, out DialogComponent? _), Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text);
                Assert.That(labels, Does.Contain(Loc.GetString("cmu-yautja-hunt-escape-console-nonyautja")));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, user, console);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleMaskScanRequiresHeldMaskLikeCmss13Attackby()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid console = default;
        EntityUid mask = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                server.PlayerMan.SetAttachedEntity(session, user);

                var interact = new InteractUsingEvent(
                    user,
                    mask,
                    console,
                    entMan.GetComponent<TransformComponent>(console).Coordinates);
                entMan.EventBus.RaiseLocalEvent(console, interact);

                Assert.That(interact.Handled, Is.True);
                Assert.That(HasActiveEscapeScanDoAfter(entMan, user), Is.False);
                Assert.That(entMan.GetComponent<YautjaHuntEscapeConsoleComponent>(console).Opened, Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text);
                Assert.That(labels, Does.Contain(
                    Loc.GetString("cmu-yautja-hunt-escape-console-mask-not-held", ("item", "clan mask"))));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, user, console, mask);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleRejectsWrongHeldItemWithCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid console = default;
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

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);
                crowbar = entMan.SpawnEntity("Crowbar", map.GridCoords);

                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                server.PlayerMan.SetAttachedEntity(session, user);
                Assert.That(hands.TryPickupAnyHand(user, crowbar), Is.True);

                var interact = new InteractUsingEvent(
                    user,
                    crowbar,
                    console,
                    entMan.GetComponent<TransformComponent>(console).Coordinates);
                entMan.EventBus.RaiseLocalEvent(console, interact);

                Assert.That(interact.Handled, Is.True);
                Assert.That(HasActiveEscapeScanDoAfter(entMan, user), Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text);
                Assert.That(labels, Does.Contain(
                    Loc.GetString("cmu-yautja-hunt-escape-console-mask-refused", ("item", "crowbar"))));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, user, console, crowbar);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleMaskScanStartAndCancelUseCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid console = default;
        EntityUid cancelledMask = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var doAfter = entMan.System<SharedDoAfterSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);
                cancelledMask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                server.PlayerMan.SetAttachedEntity(session, user);

                Assert.That(hands.TryPickupAnyHand(user, cancelledMask), Is.True);

                var cancelInteract = new InteractUsingEvent(
                    user,
                    cancelledMask,
                    console,
                    entMan.GetComponent<TransformComponent>(console).Coordinates);
                entMan.EventBus.RaiseLocalEvent(console, cancelInteract);

                Assert.That(cancelInteract.Handled, Is.True);
                var active = GetActiveEscapeScanDoAfter(entMan, user);
                Assert.That(active.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(15)));
                doAfter.Cancel(user, active.Index);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain(
                        Loc.GetString("cmu-yautja-hunt-escape-console-scan-start", ("item", "clan mask"))));
                    Assert.That(labels, Does.Contain(Loc.GetString("cmu-yautja-hunt-escape-console-scan-cancelled")));
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
                DeleteAll(entMan, user, console, cancelledMask);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleMaskScanSuccessOpensShuttersWithCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid console = default;
        EntityUid mask = default;
        EntityUid shutter = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);
                shutter = entMan.SpawnEntity("CMUHunterShipObjStructureMachineryDoorPoddoorHybrisaOpenShuttersAlmayerPdoor1EastId1", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                entMan.EnsureComponent<YautjaPreserveShutterComponent>(shutter);
                server.PlayerMan.SetAttachedEntity(session, user);

                Assert.That(hands.TryPickupAnyHand(user, mask), Is.True);

                var interact = new InteractUsingEvent(
                    user,
                    mask,
                    console,
                    entMan.GetComponent<TransformComponent>(console).Coordinates);
                entMan.EventBus.RaiseLocalEvent(console, interact);

                Assert.That(interact.Handled, Is.True);
                Assert.That(GetActiveEscapeScanDoAfter(entMan, user).Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(15)));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(15.5f));
            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<YautjaHuntEscapeConsoleComponent>(console).Opened, Is.True);
                    Assert.That(entMan.GetComponent<DoorComponent>(shutter).State, Is.Not.EqualTo(DoorState.Closed));
                    Assert.That(HasActiveEscapeScanDoAfter(entMan, user), Is.False);
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text);
                Assert.That(labels, Does.Contain(Loc.GetString("cmu-yautja-hunt-escape-console-scan-success")));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, user, console, mask, shutter);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleMaskScanBroadcastsToYautjaLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid hunter = default;
        EntityUid console = default;
        EntityUid mask = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(hands.TryPickupAnyHand(user, mask), Is.True);

                var interact = new InteractUsingEvent(
                    user,
                    mask,
                    console,
                    entMan.GetComponent<TransformComponent>(console).Coordinates);
                entMan.EventBus.RaiseLocalEvent(console, interact);

                Assert.That(interact.Handled, Is.True);
                Assert.That(HasActiveEscapeScanDoAfter(entMan, user), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text);
                Assert.That(labels, Does.Contain(
                    Loc.GetString("cmu-yautja-hunt-escape-console-scan-broadcast", ("area", "the hunting grounds"))));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, user, hunter, console, mask);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleYautjaOpenBroadcastUsesActorNameLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid observer = default;
        EntityUid console = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                observer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(observer);
                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                entMan.System<MetaDataSystem>().SetEntityName(hunter, "Test Hunter");
                server.PlayerMan.SetAttachedEntity(session, observer);

                entMan.EventBus.RaiseLocalEvent(console,
                    new YautjaHuntEscapeActionSelectedEvent(entMan.GetNetEntity(hunter), YautjaHuntEscapeAction.Open));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text);
                Assert.That(labels, Does.Contain(
                    Loc.GetString("cmu-yautja-hunt-escape-console-opened-by-yautja-broadcast", ("hunter", "Test Hunter"))));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, observer, console);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleYautjaDuplicateOpenCloseUseCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
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

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var user = entMan.GetNetEntity(hunter);
                entMan.EventBus.RaiseLocalEvent(console,
                    new YautjaHuntEscapeActionSelectedEvent(user, YautjaHuntEscapeAction.Open));
                entMan.EventBus.RaiseLocalEvent(console,
                    new YautjaHuntEscapeActionSelectedEvent(user, YautjaHuntEscapeAction.Open));
                entMan.EventBus.RaiseLocalEvent(console,
                    new YautjaHuntEscapeActionSelectedEvent(user, YautjaHuntEscapeAction.Close));
                entMan.EventBus.RaiseLocalEvent(console,
                    new YautjaHuntEscapeActionSelectedEvent(user, YautjaHuntEscapeAction.Close));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("The shutter is already open."));
                    Assert.That(labels, Does.Contain("The shutter is already closed."));
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
                DeleteAll(entMan, hunter, console);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleMaskScanAlreadyOpenUsesCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid console = default;
        EntityUid mask = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                var consoleComp = entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                consoleComp.Opened = true;
                server.PlayerMan.SetAttachedEntity(session, user);

                Assert.That(hands.TryPickupAnyHand(user, mask), Is.True);

                var interact = new InteractUsingEvent(
                    user,
                    mask,
                    console,
                    entMan.GetComponent<TransformComponent>(console).Coordinates);
                entMan.EventBus.RaiseLocalEvent(console, interact);

                Assert.That(interact.Handled, Is.True);
                Assert.That(HasActiveEscapeScanDoAfter(entMan, user), Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text);
                Assert.That(labels, Does.Contain("The shutter is already open."));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, user, console, mask);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleBusyMaskScanDoesNotCancelExistingScanLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid console = default;
        EntityUid mask = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity(null, map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                server.PlayerMan.SetAttachedEntity(session, user);
                Assert.That(hands.TryPickupAnyHand(user, mask), Is.True);

                var firstInteract = new InteractUsingEvent(
                    user,
                    mask,
                    console,
                    entMan.GetComponent<TransformComponent>(console).Coordinates);
                entMan.EventBus.RaiseLocalEvent(console, firstInteract);

                Assert.That(firstInteract.Handled, Is.True);
                var first = GetActiveEscapeScanDoAfter(entMan, user);

                var secondInteract = new InteractUsingEvent(
                    user,
                    mask,
                    console,
                    entMan.GetComponent<TransformComponent>(console).Coordinates);
                entMan.EventBus.RaiseLocalEvent(console, secondInteract);

                Assert.That(secondInteract.Handled, Is.True);
                var active = GetActiveEscapeScanDoAfter(entMan, user);
                Assert.Multiple(() =>
                {
                    Assert.That(active.Index, Is.EqualTo(first.Index));
                    Assert.That(active.Cancelled, Is.False);
                    Assert.That(active.Completed, Is.False);
                    Assert.That(entMan.GetComponent<YautjaHuntEscapeConsoleComponent>(console).Opened, Is.False);
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
                DeleteAll(entMan, user, console, mask);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleOpenAndScanDoNotWriteAdminLogsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            AdminLogsEnabled = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var adminLogs = server.ResolveDependency<IAdminLogManager>();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid yautjaConsole = default;
        EntityUid scanConsole = default;
        EntityUid mask = default;
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
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                yautjaConsole = entMan.SpawnEntity(null, map.GridCoords);
                scanConsole = entMan.SpawnEntity(null, map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(yautjaConsole);
                var scanConsoleComp = entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(scanConsole);
                scanConsoleComp.MaskScanDelay = TimeSpan.Zero;
                Assert.That(hands.TryPickupAnyHand(prey, mask), Is.True);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                entMan.EventBus.RaiseLocalEvent(yautjaConsole,
                    new YautjaHuntEscapeActionSelectedEvent(entMan.GetNetEntity(hunter), YautjaHuntEscapeAction.Open));

                var interact = new InteractUsingEvent(
                    prey,
                    mask,
                    scanConsole,
                    entMan.GetComponent<TransformComponent>(scanConsole).Coordinates);
                entMan.EventBus.RaiseLocalEvent(scanConsole, interact);
                Assert.That(interact.Handled, Is.True);
                Assert.That(scanConsoleComp.Opened, Is.True);
            });

            var logs = await adminLogs.CurrentRoundLogs(new LogFilter
            {
                Types = new HashSet<LogType> { LogType.Action },
            });
            var messages = logs.Select(log => log.Message).ToList();
            var joinedMessages = string.Join("\n", messages);

            Assert.That(
                messages,
                Has.None.Contains("Yautja hunting preserve shutters").IgnoreCase,
                $"CMSS13 preserve shutter console sends global signals and Yautja broadcasts, but does not write admin logs.\nActual logs:\n{joinedMessages}");
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, prey, yautjaConsole, scanConsole, mask);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static bool HasActiveEscapeScanDoAfter(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? component) &&
               component.DoAfters.Values.Any(active =>
                   !active.Cancelled &&
                   !active.Completed &&
                   active.Args.Event is YautjaHuntEscapeScanDoAfterEvent);
    }

    private static DoAfter GetActiveEscapeScanDoAfter(IEntityManager entMan, EntityUid user)
    {
        return entMan.GetComponent<DoAfterComponent>(user)
            .DoAfters.Values
            .Single(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaHuntEscapeScanDoAfterEvent);
    }

    private static void DeleteAll(IEntityManager entMan, params EntityUid[] entities)
    {
        foreach (var uid in entities)
        {
            if (uid != default && !entMan.Deleted(uid))
                entMan.DeleteEntity(uid);
        }
    }
}
