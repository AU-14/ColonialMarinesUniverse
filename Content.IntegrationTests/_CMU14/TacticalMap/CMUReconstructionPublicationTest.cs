using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server._RMC14.TacticalMap;
using Content.Server._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Fixture setup of authorized faction feeds.
    [Test]
    public async Task UnrecognizedConsoleFactionCannotReadOrWriteMarineCanvas()
    {
        await Server.WaitAssertion(() =>
        {
            _ui.CloseUi(_console, Key, _actor);
            var map = SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
            map.MarineLines = [new(default, default, Color.Blue, 3, [new(2, 3)])];
            SEntMan.System<TacticalMapSystem>().SetComputerFaction((_console, SComp<TacticalMapComputerComponent>(_console)), "unrecognized");
            Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True);
            Send(new CMUReconViewMessage(Vector2i.Zero));
            var scene = _recon.BuildSnapshot(_console, _actor)!;
            Assert.That(scene.CanOrder, Is.False);
            Assert.That(scene.Orders, Is.Empty);
            Send(new CMUReconSendMessage(scene.Generation, 1, [new(0, [new(4, 5)], CMUReconInk.Red)], []));
            Assert.That(map.MarineLines, Has.Count.EqualTo(1));
            Assert.That(map.MarineLines[0].Color, Is.EqualTo(Color.Blue));
        });
    }

    [Test]
    public async Task OverwatchDrawingReachesOnlyItsAssignedSquad()
    {
        var cleanup = new List<EntityUid>();
        try
        {
            await Server.WaitAssertion(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                var map = SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                var squad = SEntMan.SpawnEntity("SquadGovforBravo", MapCoordinates.Nullspace);
                var other = SEntMan.SpawnEntity("SquadGovforCharlie", MapCoordinates.Nullspace);
                cleanup.AddRange([squad, other]);
                SEntMan.EnsureComponent<OverwatchConsoleComponent>(_console).Squad = SEntMan.GetNetEntity(squad);
                Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True);
                Send(new CMUReconViewMessage(Vector2i.Zero));
                Send(new CMUReconSendMessage(_recon.BuildSnapshot(_console, _actor)!.Generation, 1,
                    [new(0, [new(2, 3)], CMUReconInk.Green)], []));
                Assert.That(map.GovforLines, Is.Empty, "Squad plans must not overwrite the global faction canvas.");
                Assert.That(SComp<SquadTeamComponent>(squad).TacMapLines, Has.Count.EqualTo(1));
                Assert.That(SComp<SquadTeamComponent>(other).TacMapLines, Is.Empty);
                foreach (var team in new[] { squad, other })
                {
                    var member = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(3.5f)));
                    cleanup.Add(member);
                    SEntMan.System<SquadSystem>().AssignSquad(member, team, null);
                    var user = SComp<TacticalMapUserComponent>(member);
                    user.Marines = false; user.Govfor = true; user.HasSquad = true;
                    SEntMan.EventBus.RaiseLocalEvent(member, new OpenTacticalMapActionEvent { Performer = member });
                    var request = new CMUReconViewMessage(Vector2i.Zero) { Actor = member, UiKey = TacticalMapUserUi.Key };
                    SEntMan.EventBus.RaiseLocalEvent(member, (object) request);
                    Assert.That(_recon.BuildSnapshot(member, member)!.Orders.Length, Is.EqualTo(team == squad ? 1 : 0));
                    _ui.CloseUi(member, TacticalMapUserUi.Key, member);
                }
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                foreach (var entity in cleanup.AsEnumerable().Reverse())
                    if (SEntMan.EntityExists(entity)) SEntMan.DeleteEntity(entity);
            });
        }
    }

    [TestCase("GOVFOR", "GOVFOR", true)]
    [TestCase("GOVFOR", "OPFOR", false)]
    [TestCase("OPFOR", "OPFOR", true)]
    [TestCase("OPFOR", "GOVFOR", false)]
    [TestCase("MARINES", "MARINES", true)]
    [TestCase("CLF", "CLF", true)]
    [TestCase("WEYU", "WEYU", true)]
    public async Task PublishedCanvasAndAnnouncementReachOnlyAuthorizedFaction(string senderFaction, string receiverFaction, bool receives)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid receiver = default;
        var history = 0;
        try
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                var tactical = SEntMan.System<TacticalMapSystem>();
                tactical.SetComputerFaction((_console, SComp<TacticalMapComputerComponent>(_console)), senderFaction);
                receiver = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(2.5f)));
                SEntMan.EnsureComponent<MarineComponent>(receiver).Faction = receiverFaction;
                Server.PlayerMan.SetAttachedEntity(session, receiver);
            });
            await Pair.RunUntilSynced();
            await Client.WaitPost(() => history = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>().History.Count);
            await Server.WaitAssertion(() =>
            {
                var user = SComp<TacticalMapUserComponent>(receiver);
                user.Marines = receiverFaction == "MARINES";
                user.Govfor = receiverFaction == "GOVFOR";
                user.Opfor = receiverFaction == "OPFOR";
                user.Clf = receiverFaction == "CLF";
                user.WeYu = receiverFaction == "WEYU";
                user.Xenos = false;
                SEntMan.EventBus.RaiseLocalEvent(receiver, new OpenTacticalMapActionEvent { Performer = receiver });
                Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True);
                Send(new CMUReconViewMessage(Vector2i.Zero));
                var scene = _recon.BuildSnapshot(_console, _actor)!;
                Send(new CMUReconSendMessage(scene.Generation, 1, [new(0, [new(2, 3), new(4, 5)], CMUReconInk.Blue)], []));
                var tactical = SEntMan.System<TacticalMapSystem>();
                foreach (var faction in new[] { "MARINES", "GOVFOR", "OPFOR", "CLF", "WEYU", "XENONIDS" })
                {
                    Assert.That(tactical.TryReconstructionCanvas(_upper, faction, out var lines, out _), Is.True);
                    Assert.That(lines.Count, Is.EqualTo(faction == senderFaction ? 1 : 0), "A Send must change only its authorized faction canvas.");
                }
            });
            await Pair.RunTicksSync(50);
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                Assert.That(window.SurveyView.Scene, Is.Not.Null);
                Assert.That(window.SurveyView.Orders.Length, Is.EqualTo(receives ? 1 : 0));
                Assert.That(window.SurveyView.Scene!.CanOrder, Is.False, "A personal human map remains read-only even when its faction can publish from consoles.");
                var messages = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>().History.Skip(history);
                Assert.That(messages.Any(m => m.Msg.Message.Contains($"The {senderFaction} tactical map has been updated.")), Is.EqualTo(receives));
            });
            await Server.WaitAssertion(() =>
            {
                // A forged Send from the ordinary personal action must fail on the server.
                var scene = _recon.BuildSnapshot(receiver, receiver)!;
                var send = new CMUReconSendMessage(scene.Generation, 42, [new(0, [new(6, 7)], CMUReconInk.Green)], [])
                    { Actor = receiver, UiKey = TacticalMapUserUi.Key };
                SEntMan.EventBus.RaiseLocalEvent(receiver, (object) send);
                Assert.That(_recon.BuildSnapshot(receiver, receiver)!.Orders.Length, Is.EqualTo(receives ? 1 : 0));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (receiver.IsValid()) _ui.CloseUi(receiver, TacticalMapUserUi.Key, receiver);
                Server.PlayerMan.SetAttachedEntity(session, original);
                if (receiver.IsValid()) SEntMan.DeleteEntity(receiver);
            });
            await Pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task FreshObserverReceivesReconstructionAndAllFactionDrawingsButCannotPublish()
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid ghost = default;
        try
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                var map = SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                map.GovforLines = [new(default, default, Color.Blue, 3, [new(2, 3)])];
                map.OpforLines = [new(default, default, Color.Red, 3, [new(4, 5)])];
                ghost = SEntMan.SpawnEntity("MobObserver", new EntityCoordinates(_upper, new Vector2(1.5f)));
                Server.PlayerMan.SetAttachedEntity(session, ghost);
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(ghost, new OpenTacticalMapActionEvent { Performer = ghost }));
            await Pair.RunTicksSync(50);
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                Assert.That(window.SurveyView.Scene, Is.Not.Null, "The ghost's survey request must pass through the network BUI validation.");
                Assert.That(window.SurveyView.Scene!.LoadedChunks, Is.EqualTo(window.SurveyView.Scene.TotalChunks));
                Assert.That(window.SurveyView.Scene.CanOrder, Is.False);
                Assert.That(window.SurveyView.Orders.Select(o => o.Color), Is.EquivalentTo(new[] { Color.Blue, Color.Red }));
            });
            await Server.WaitAssertion(() =>
            {
                // Even a stale or wrongly configured CanDraw flag must not authorize spectators.
                SComp<TacticalMapUserComponent>(ghost).CanDraw = true;
                var scene = _recon.BuildSnapshot(ghost, ghost)!;
                var forged = new CMUReconSendMessage(scene.Generation, 1, [new(0, [new(1, 2)], CMUReconInk.Green)], [])
                    { Actor = ghost, UiKey = TacticalMapUserUi.Key };
                SEntMan.EventBus.RaiseLocalEvent(ghost, (object) forged);
                Assert.That(_recon.BuildSnapshot(ghost, ghost)!.Orders, Has.Length.EqualTo(2));
                Assert.That(SComp<TacticalMapComponent>(_upper).GovforLines, Has.Count.EqualTo(1));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (ghost.IsValid()) _ui.CloseUi(ghost, TacticalMapUserUi.Key, ghost);
                Server.PlayerMan.SetAttachedEntity(session, original);
                if (ghost.IsValid()) SEntMan.DeleteEntity(ghost);
            });
            await Pair.RunUntilSynced();
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task QueenPublicationAnnouncesToHerHive(bool sameHive)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid queen = default, receiver = default, hive = default, otherHive = default;
        var history = 0;
        try
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                hive = SEntMan.SpawnEntity(null, new EntityCoordinates(_upper, Vector2.Zero));
                otherHive = SEntMan.SpawnEntity(null, new EntityCoordinates(_upper, Vector2.Zero));
                SEntMan.EnsureComponent<HiveComponent>(hive);
                SEntMan.EnsureComponent<HiveComponent>(otherHive);
                queen = SEntMan.SpawnEntity("CMXenoQueen", new EntityCoordinates(_upper, new Vector2(3.5f)));
                receiver = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(_upper, new Vector2(5.5f)));
                var hives = SEntMan.System<XenoHiveSystem>();
                hives.SetHive(queen, hive);
                hives.SetHive(receiver, sameHive ? hive : otherHive);
                Server.PlayerMan.SetAttachedEntity(session, receiver);
            });
            await Pair.RunUntilSynced();
            await Client.WaitPost(() => history = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>().History.Count);
            await Server.WaitAssertion(() =>
            {
                SEntMan.EventBus.RaiseLocalEvent(queen, new OpenTacticalMapActionEvent { Performer = queen });
                var view = new CMUReconViewMessage(Vector2i.Zero) { Actor = queen, UiKey = TacticalMapUserUi.Key };
                SEntMan.EventBus.RaiseLocalEvent(queen, (object) view);
                var scene = _recon.BuildSnapshot(queen, queen)!;
                Assert.That(scene.CanOrder, Is.True);
                var send = new CMUReconSendMessage(scene.Generation, 1, [new(0, [new(2, 3)], CMUReconInk.Purple)], [])
                    { Actor = queen, UiKey = TacticalMapUserUi.Key };
                SEntMan.EventBus.RaiseLocalEvent(queen, (object) send);
                var map = SComp<TacticalMapComponent>(_upper);
                Assert.That(map.XenoLines, Has.Count.EqualTo(1));
                Assert.That(map.GovforLines, Is.Empty);
                Assert.That(map.OpforLines, Is.Empty);
                Assert.That(map.LastUpdateXenoBlips, Is.EquivalentTo(map.XenoBlips), "Publishing also refreshes the normal last-update contact snapshot.");
            });
            await Pair.RunTicksSync(25);
            await Client.WaitAssertion(() =>
            {
                var messages = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>().History.Skip(history);
                Assert.That(messages.Any(m => m.Msg.Message.Contains("The mental map sharpens.")), Is.EqualTo(sameHive));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (queen.IsValid()) _ui.CloseUi(queen, TacticalMapUserUi.Key, queen);
                Server.PlayerMan.SetAttachedEntity(session, original);
                foreach (var uid in new[] { receiver, queen, hive, otherHive })
                    if (uid.IsValid() && SEntMan.EntityExists(uid)) SEntMan.DeleteEntity(uid);
            });
            await Pair.RunUntilSynced();
        }
    }
#pragma warning restore RA0002
}
