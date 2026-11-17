using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Popups;
using Content.Server.Administration.Logs;
using Content.Server.Power.Components;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Power;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Gravity;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaPreserveConsoleTest
{

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
                var mapSystem = entMan.System<SharedMapSystem>();
                mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), map.Tile.Tile);
                mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(2, 0), map.Tile.Tile);
                mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(3, 0), map.Tile.Tile);
                entMan.EnsureComponent<GravityComponent>(map.Grid).Enabled = true;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
                hunterEdge = entMan.SpawnEntity("CMUYautjaHuntingGroundPreserveEdge", map.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
                preyEdge = entMan.SpawnEntity("CMUYautjaHuntingGroundPreserveEdge", map.GridCoords.Offset(new Vector2(3.5f, 0.5f)));
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
