using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Camera;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.ARES.ExternalTerminals;
using Content.Shared._RMC14.Camera;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.OrbitalCannon;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Camera;
using Content.Shared.CMU14.util;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Hijack;

public sealed partial class AlmayerHijackMapTest
{
    [TestCase("USCM")]
    [TestCase("WEYU")]
    public async Task ShipConsolesUseGovforAndReachEquipmentAcrossDecks(string platoonId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var platoon = server.ProtoMan.Index<PlatoonPrototype>(platoonId);
            server.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon = platoon;
            var map = LoadAlmayer(pair);
            var maps = server.System<CMUZLevelsSystem>().GetAllNetworkMaps(map).ToHashSet();
            var consoles = new List<EntityUid>();
            var query = entities.AllEntityQueryEnumerator<TransformComponent>();
            while (query.MoveNext(out var uid, out var transform))
                if (transform.MapUid is { } owner && maps.Contains(owner))
                    consoles.Add(uid);

            var actor = entities.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
            entities.AddComponent<AccessComponent>(actor);
            var access = server.System<AccessReaderSystem>();
            var cards = server.System<SharedAccessSystem>();
            var ui = server.System<SharedUserInterfaceSystem>();
            var requisitions = consoles.Where(entities.HasComponent<RequisitionsComputerComponent>).ToArray();
            Assert.That(requisitions, Has.Length.EqualTo(5));
            var catalog = (RequisitionsComputerComponent) server.ProtoMan.Index<EntityPrototype>(platoon.Reqlist)
                .Components["RequisitionsComputer"].Component;
            var accounts = new HashSet<EntityUid>();
            foreach (var uid in requisitions)
            {
                var computer = entities.GetComponent<RequisitionsComputerComponent>(uid);
                Assert.That(computer.Faction, Is.EqualTo("govfor"));
                Assert.That(computer.Account, Is.Not.Null);
                accounts.Add(computer.Account!.Value);
                Assert.That(entities.GetComponent<RequisitionsAccountComponent>(computer.Account.Value).Faction,
                    Is.EqualTo("govfor"));
                Assert.That(computer.Categories.Take(catalog.Categories.Count), Is.EqualTo(catalog.Categories));
                cards.TrySetTags(actor, new ProtoId<AccessLevelPrototype>[] { "AU14AccessGovforCommand" });
                Assert.That(access.IsAllowed(actor, uid), Is.True);
                cards.TrySetTags(actor, new ProtoId<AccessLevelPrototype>[] { "AU14AccessOpforCommand" });
                Assert.That(access.IsAllowed(actor, uid), Is.False);
                entities.EventBus.RaiseLocalEvent(uid, new BeforeActivatableUIOpenEvent(actor));
                Assert.That(ui.TryGetUiState<RequisitionsBuiState>(uid, RequisitionsUIKey.Key, out var state), Is.True);
                Assert.That(state!.PlatformLowered, Is.Not.Null, "ASRS must find its delivery elevator.");
                Assert.That(state.AvailableSlots, Is.GreaterThan(0));
            }
            Assert.That(accounts, Has.Count.EqualTo(1));

            var techConsoles = consoles.Where(entities.HasComponent<TechControlConsoleComponent>).ToArray();
            Assert.That(techConsoles, Has.Length.EqualTo(3));
            foreach (var uid in techConsoles)
            {
                var tech = entities.GetComponent<TechControlConsoleComponent>(uid);
                Assert.That(tech.Team, Is.EqualTo("govfor"));
                entities.EventBus.RaiseLocalEvent(uid, new BeforeActivatableUIOpenEvent(actor));
                Assert.That(tech.Tree.Options, Is.Not.Empty);
            }
            var intel = consoles.Where(entities.HasComponent<IntelConsoleComponent>).ToArray();
            Assert.That(intel, Has.Length.EqualTo(3));
            foreach (var uid in intel)
                Assert.That(entities.GetComponent<IntelConsoleComponent>(uid).Team, Is.EqualTo("govfor"));

            var govfor = entities.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
            var opfor = entities.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
#pragma warning disable RA0002 // Test squads exercise the actual Overwatch team filter.
            entities.AddComponent<SquadTeamComponent>(govfor).Group = "GOVFOR";
            entities.AddComponent<SquadTeamComponent>(opfor).Group = "OPFOR";
#pragma warning restore RA0002
            var overwatch = consoles.Where(entities.HasComponent<OverwatchConsoleComponent>).ToArray();
            Assert.That(overwatch, Has.Length.EqualTo(7));
            foreach (var uid in overwatch)
            {
                var computer = entities.GetComponent<OverwatchConsoleComponent>(uid);
                Assert.That(computer.Group, Is.EqualTo("GOVFOR"));
                Assert.That(entities.GetComponent<TacticalMapComputerComponent>(uid).Faction, Is.EqualTo("govfor"));
                var squads = server.System<SharedOverwatchConsoleSystem>().GetOverwatchBuiState((uid, computer)).Squads;
                Assert.That(squads.Select(s => s.Id), Does.Contain(entities.GetNetEntity(govfor)));
                Assert.That(squads.Select(s => s.Id), Does.Not.Contain(entities.GetNetEntity(opfor)));
                Assert.That(server.System<OrbitalCannonSystem>().TryGetClosestCannon(uid, out _), Is.True);
            }

            // Televisions use a separate broadcast channel, which may legitimately have no transmitter.
            var monitors = consoles.Where(entities.HasComponent<RMCCameraComputerComponent>)
                .Where(uid => entities.GetComponent<CameraNetworkReceiverComponent>(uid).Networks
                    .Contains("RMCSurveillanceCameraAlmayer")).ToArray();
            Assert.That(monitors.Length, Is.GreaterThan(40));
            foreach (var uid in monitors)
            {
                var receiver = entities.GetComponent<CameraNetworkReceiverComponent>(uid);
                var cameras = server.System<CameraNetworkSystem>().GetAccessibleCameras((uid, receiver));
                Assert.That(cameras, Is.Not.Empty,
                    $"Camera terminal {uid} ({entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID}) has no feeds.");
                Assert.That(cameras.Any(camera => entities.GetComponent<TransformComponent>(camera).MapUid !=
                    entities.GetComponent<TransformComponent>(uid).MapUid), Is.True, "Ship feeds must span decks.");
            }

            var terminals = consoles.Where(entities.HasComponent<DropshipTerminalComponent>).ToArray();
            Assert.That(terminals, Has.Length.EqualTo(2));
            var landingZones = new HashSet<EntityUid>();
            foreach (var uid in terminals)
            {
                var terminal = entities.GetComponent<DropshipTerminalComponent>(uid);
                Assert.That(terminal.UseShipDestinations, Is.True);
                var lz = server.System<SharedDropshipSystem>().FindTerminalLZ((uid, terminal));
                Assert.That(lz, Is.Not.Null);
                Assert.That(lz!.Value.Comp1.Home, Is.True);
                Assert.That(lz.Value.Comp1.FactionController, Is.EqualTo("govfor"));
                Assert.That(lz.Value.Comp2.MapUid, Is.EqualTo(map));
                Assert.That(entities.GetComponent<TransformComponent>(uid).MapUid, Is.Not.EqualTo(map));
                Assert.That(entities.HasComponent<DropshipHijackDestinationComponent>(lz.Value.Owner), Is.False);
                landingZones.Add(lz.Value.Owner);
            }
            Assert.That(landingZones, Has.Count.EqualTo(2), "Each hangar must have its own recall terminal.");

            var idCards = server.System<SharedIdCardSystem>();
            var govforCard = entities.SpawnEntity("AU14IDCardGOVFORPlatCo", new EntityCoordinates(map, Vector2.Zero));
            var opforCard = entities.SpawnEntity("AU14IDCardOPFORPlatCo", new EntityCoordinates(map, Vector2.Zero));
            foreach (var card in new[] { govforCard, opforCard })
            {
                idCards.TryChangeFullName(card, "Console Test");
                idCards.TryChangeJobTitle(card, "Commander");
            }
            cards.TrySetTags(govforCard, new ProtoId<AccessLevelPrototype>[] { "AU14AccessGovforCommand" });
            var maintenance = consoles.Where(entities.HasComponent<ARESExternalTerminalComponent>).ToArray();
            Assert.That(maintenance.Length, Is.GreaterThanOrEqualTo(15));
            foreach (var uid in maintenance)
            {
                var terminal = entities.GetComponent<ARESExternalTerminalComponent>(uid);
                entities.EventBus.RaiseLocalEvent(uid, new BeforeActivatableUIOpenEvent(govforCard));
                Assert.That(terminal.ARESCore, Is.Not.Null, "Maintenance terminal must find a real log core.");
                entities.EventBus.RaiseLocalEvent(uid, new RMCARESExternalLogin
                    { Actor = opforCard, UiKey = ARESExternalTerminalUIKey.Key });
                Assert.That(terminal.LoggedIn, Is.False, "Opfor cards cannot log into Govfor terminals.");
                entities.EventBus.RaiseLocalEvent(uid, new RMCARESExternalLogin
                    { Actor = govforCard, UiKey = ARESExternalTerminalUIKey.Key });
                Assert.That(terminal.LoggedIn, Is.True, "The actual Govfor ID must work without legacy marine IFF.");
                Assert.That(terminal.ShownLogs.Select(log => log.Id), Does.Contain("ARESTabAnnouncementLogs"));
                Assert.That(terminal.ShownLogs.Select(log => log.Id), Does.Not.Contain("ARESTabMedicalLogs"));
                var core = server.System<ARESCoreSystem>();
                core.CreateARESLog(uid, "ARESTabAnnouncementLogs", $"Deck terminal {uid}");
                Assert.That(core.PullARESLogs(terminal.ARESCore!.Value, "ARESTabAnnouncementLogs", out var logs), Is.True);
                Assert.That(logs!.Last(), Does.Contain($"Deck terminal {uid}"));
            }
        });
        await pair.CleanReturnAsync();
    }
}
