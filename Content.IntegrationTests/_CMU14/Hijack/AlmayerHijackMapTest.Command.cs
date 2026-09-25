using System.Linq;
using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.ControlComputer;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Inventory;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Hijack;

public sealed partial class AlmayerHijackMapTest
{
    [Test]
    public async Task OverwatchWatchesAcrossDecksAndShipAnnouncementsReachAllDecks()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var sessions = await server.AddDummySessions(8);
        await server.WaitRunTicks(3);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var lower = LoadAlmayer(pair);
            var decks = server.System<CMUZLevelsSystem>().GetAllNetworkMaps(lower).ToArray();
            for (var i = 0; i < decks.Length; i++)
            {
                var listener = entities.SpawnEntity(null, new EntityCoordinates(decks[i], Vector2.Zero));
                entities.AddComponent<MarineComponent>(listener).Faction = "govfor";
                server.PlayerMan.SetAttachedEntity(sessions[i], listener);
            }
            var enemy = entities.SpawnEntity(null, new EntityCoordinates(lower, Vector2.Zero));
            entities.AddComponent<MarineComponent>(enemy).Faction = "opfor";
            server.PlayerMan.SetAttachedEntity(sessions[5], enemy);
            var elsewhere = server.System<SharedMapSystem>().CreateMap();
            var away = entities.SpawnEntity(null, new EntityCoordinates(elsewhere, Vector2.Zero));
            entities.AddComponent<MarineComponent>(away).Faction = "govfor";
            server.PlayerMan.SetAttachedEntity(sessions[6], away);

            EntityUid commandConsole = EntityUid.Invalid;
            var commandQuery = entities.EntityQueryEnumerator<MarineControlComputerComponent, TransformComponent>();
            while (commandQuery.MoveNext(out var uid, out _, out var transform))
                if (transform.MapUid is { } map && decks.Contains(map))
                    commandConsole = uid;
            Assert.That(commandConsole.IsValid(), Is.True);
            var operatorUid = entities.SpawnEntity("MobHuman", entities.GetComponent<TransformComponent>(commandConsole).Coordinates);
            entities.EnsureComponent<MarineComponent>(operatorUid).Faction = "govfor";
            entities.EnsureComponent<AccessComponent>(operatorUid);
            server.System<SharedAccessSystem>().TrySetTags(operatorUid,
                new ProtoId<AccessLevelPrototype>[] { "AU14AccessGovforCommand" });
            server.System<SkillsSystem>().SetSkill(operatorUid, "RMCSkillOverwatch", 1);
            server.PlayerMan.SetAttachedEntity(sessions[7], operatorUid);
            var ui = server.System<SharedUserInterfaceSystem>();
            var interaction = server.System<ActivatableUISystem>();
            Assert.That(interaction.InteractUI(operatorUid, commandConsole), Is.True);
            Assert.That(ui.IsUiOpen(commandConsole, MarineControlComputerUi.Key, operatorUid), Is.True);
            var control = entities.GetComponent<MarineControlComputerComponent>(commandConsole);
            var recipients = server.System<SharedMarineControlComputerSystem>()
                .GetShipAnnouncementFilter((commandConsole, control)).Recipients.ToHashSet();
            Assert.That(recipients, Is.EquivalentTo(sessions.Take(5).Append(sessions[7])),
                "Shipwide includes all five Govfor decks, excludes Opfor and Govfor on another map.");
            var message = new MarineControlComputerShipAnnouncementDialogEvent(entities.GetNetEntity(operatorUid),
                "ALMAYER SHIPWIDE REGRESSION");
            entities.EventBus.RaiseLocalEvent(commandConsole, message);
            Assert.That(control.LastShipAnnouncement, Is.Not.Null, "The actual announcement handler must run.");
            var core = server.System<ARESCoreSystem>();
            Assert.That(core.TryGetARES(commandConsole, out var ares), Is.True);
            Assert.That(core.PullARESLogs(ares!.Value.Owner, "ARESTabAnnouncementLogs", out var logs), Is.True);
            Assert.That(logs!.Last(), Does.Contain(message.Message));
            ui.CloseUi(commandConsole, MarineControlComputerUi.Key, operatorUid);

            var squad = entities.SpawnEntity("SquadGovfor", new EntityCoordinates(lower, Vector2.Zero));
            var enemySquad = entities.SpawnEntity("SquadOpfor", new EntityCoordinates(lower, Vector2.Zero));
            var marine = entities.SpawnEntity("MobHuman", new EntityCoordinates(lower, Vector2.Zero));
            entities.EnsureComponent<MarineComponent>(marine).Faction = "govfor";
            server.System<SquadSystem>().AssignSquad(marine, squad, "AU14JobGOVFORSquadRifleman");
            var helmet = entities.SpawnEntity("ArmorHelmetM10", new EntityCoordinates(lower, Vector2.Zero));
            Assert.That(entities.HasComponent<OverwatchCameraComponent>(helmet), Is.True);
            Assert.That(server.System<InventorySystem>().TryEquip(marine, helmet, "head", silent: true, force: true), Is.True);
            var consoles = entities.EntityQueryEnumerator<OverwatchConsoleComponent, TransformComponent>();
            var checkedConsoles = 0;
            while (consoles.MoveNext(out var uid, out var console, out var transform))
            {
                if (transform.MapUid is not { } map || !decks.Contains(map))
                    continue;
                server.System<SharedTransformSystem>().SetCoordinates(operatorUid, transform.Coordinates);
                Assert.That(interaction.InteractUI(operatorUid, uid), Is.True, $"Cannot open Overwatch {uid}");
                // Groundside exposes Overwatch through a button in its primary communications UI.
                if (entities.HasComponent<MarineCommunicationsComputerComponent>(uid))
                    entities.EventBus.RaiseLocalEvent(uid, new MarineCommunicationsOverwatchMsg
                        { Actor = operatorUid, UiKey = MarineCommunicationsComputerUI.Key });
                Assert.That(ui.IsUiOpen(uid, OverwatchConsoleUI.Key, operatorUid), Is.True);
                var state = server.System<SharedOverwatchConsoleSystem>().GetOverwatchBuiState((uid, console));
                Assert.That(state.Squads.Select(s => s.Id), Does.Contain(entities.GetNetEntity(squad)));
                Assert.That(state.Squads.Select(s => s.Id), Does.Not.Contain(entities.GetNetEntity(enemySquad)));
                entities.EventBus.RaiseLocalEvent(uid, new OverwatchConsoleSelectSquadBuiMsg(entities.GetNetEntity(squad))
                    { Actor = operatorUid, UiKey = OverwatchConsoleUI.Key });
                Assert.That(console.Squad, Is.EqualTo(entities.GetNetEntity(squad)));
                var watch = new OverwatchConsoleWatchBuiMsg(entities.GetNetEntity(marine))
                    { Actor = operatorUid, UiKey = OverwatchConsoleUI.Key };
                entities.EventBus.RaiseLocalEvent(uid, watch);
                Assert.That(entities.GetComponent<OverwatchWatchingComponent>(operatorUid).Watching, Is.EqualTo(helmet));
                Assert.That(entities.GetComponent<EyeComponent>(operatorUid).Target, Is.EqualTo(helmet));
                entities.EventBus.RaiseLocalEvent(uid, watch);
                Assert.That(entities.GetComponent<EyeComponent>(operatorUid).Target, Is.Not.EqualTo(helmet));
                ui.CloseUi(uid, OverwatchConsoleUI.Key, operatorUid);
                checkedConsoles++;
            }
            Assert.That(checkedConsoles, Is.EqualTo(7));
        });
        foreach (var session in sessions)
            await server.RemoveDummySession(session);
        await pair.CleanReturnAsync();
    }
}
