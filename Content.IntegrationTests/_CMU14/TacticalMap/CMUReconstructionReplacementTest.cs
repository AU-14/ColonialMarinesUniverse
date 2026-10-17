using Content.Client._RMC14.TacticalMap;
using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task UnassignedComputerUsesTheNormalFactionAuthorization(bool identified)
    {
        await Server.WaitAssertion(() =>
        {
            _ui.CloseUi(_console, Key, _actor);
            var computer = SComp<TacticalMapComputerComponent>(_console);
            SEntMan.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().SetComputerFaction((_console, computer), null);
            if (identified) SEntMan.EnsureComponent<Content.Shared._RMC14.Marines.MarineComponent>(_actor).Faction = "opfor";
            Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True);
            Send(new CMUReconViewMessage(Vector2i.Zero));
            var scene = _recon.BuildSnapshot(_console, _actor)!;
            Assert.That(scene.CanOrder, Is.EqualTo(identified));
            Assert.That(computer.Faction, Is.EqualTo(identified ? "OPFOR" : null));
            Send(new CMUReconSendMessage(scene.Generation, 1, [new(0, [new(2, 3)], CMUReconInk.Green)], []));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders.Length, Is.EqualTo(identified ? 1 : 0),
                "An unidentified user must not write to the default marine faction merely by having leadership.");
        });
    }

    [Test]
    public async Task PublishedStrokeReplicatesToAnOpenClassicCanvasAndBack()
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid table = default;
        NetEntity tableNet = default;
        var path = new Vector2[] { new(1.25f, 2.5f), new(4.75f, 5.25f) };
        try
        {
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, true));
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                table = SEntMan.SpawnEntity("CMUTacticalMapTableGovfor", new EntityCoordinates(_upper, new Vector2(0.5f)));
                tableNet = SEntMan.GetNetEntity(table);
                SEntMan.RemoveComponent<AccessReaderComponent>(table);
                Server.PlayerMan.SetAttachedEntity(session, _actor);
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => _ui.TryOpenUi(table, TacticalMapComputerUi.Key, _actor));
            await Pair.RunTicksSync(30);
            await Server.WaitPost(() =>
            {
                var view = new CMUReconViewMessage(Vector2i.Zero) { Actor = _actor, UiKey = TacticalMapComputerUi.Key };
                SEntMan.EventBus.RaiseLocalEvent(table, (object) view);
                var scene = _recon.BuildSnapshot(table, _actor)!;
                var send = new CMUReconSendMessage(scene.Generation, 1, [new(-1, path, CMUReconInk.Purple, 8)], [])
                    { Actor = _actor, UiKey = TacticalMapComputerUi.Key };
                SEntMan.EventBus.RaiseLocalEvent(table, (object) send);
            });
            await Pair.RunTicksSync(40);
            // This test exercises canvas interoperability, independently of announcement throttling.
            await Server.WaitPost(() =>
            {
#pragma warning disable RA0002
                SComp<TacticalMapComputerComponent>(table).NextAnnounceAt = TimeSpan.Zero;
#pragma warning restore RA0002
            });
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<TacticalMapWindow>().Single();
                Assert.That(window.Wrapper.Map.Lines.Single().WorldPoints, Is.EqualTo(path));
                Assert.That(window.Wrapper.Canvas.Lines.Single().WorldPoints, Is.EqualTo(path), "An untouched, already-open classic canvas must refresh.");
                var line = new TacticalMapLine(default, default, Color.Green, 4, [new(2.5f, 1.5f), new(6.25f, 3.75f)]);
                window.Wrapper.Canvas.Lines.Add(line);
                var ui = CEntMan.System<SharedUserInterfaceSystem>();
                Assert.That(ui.TryGetOpenUi<TacticalMapComputerBui>(CEntMan.GetEntity(tableNet), TacticalMapComputerUi.Key, out var bui), Is.True);
                bui!.SendMessage(new TacticalMapUpdateCanvasMsg(new(window.Wrapper.Canvas.Lines), new()));
            });
            await Pair.RunTicksSync(40);
            await Server.WaitAssertion(() =>
            {
                var orders = _recon.BuildSnapshot(table, _actor)!.Orders;
                Assert.That(orders, Has.Length.EqualTo(2));
                Assert.That(orders.Single(o => o.Depth == -1).Waypoints, Is.EqualTo(path));
                Assert.That(orders.Single(o => o.Color == Color.Green).Waypoints![1], Is.EqualTo(new Vector2(6.25f, 3.75f)));
                var scene = _recon.BuildSnapshot(table, _actor)!;
                var send = new CMUReconSendMessage(scene.Generation, 2, [new(0, [new(3, 3)], CMUReconInk.Blue, 3)], [])
                    { Actor = _actor, UiKey = TacticalMapComputerUi.Key };
                SEntMan.EventBus.RaiseLocalEvent(table, (object) send);
            });
            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<TacticalMapWindow>().Single();
                Assert.That(window.Wrapper.Canvas.Lines, Has.Count.EqualTo(3), "After classic Send is acknowledged, subsequent published edits must refresh the canvas again.");
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (table.IsValid()) _ui.CloseUi(table, TacticalMapComputerUi.Key, _actor);
                Server.PlayerMan.SetAttachedEntity(session, original);
            });
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, false));
            await Pair.RunUntilSynced();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RealPersonalActionHonorsClassicPreference(bool classic)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid marine = default;
        try
        {
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, classic));
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                marine = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(1.5f)));
                Server.PlayerMan.SetAttachedEntity(session, marine);
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() =>
            {
                var action = new OpenTacticalMapActionEvent { Performer = marine };
                SEntMan.EventBus.RaiseLocalEvent(marine, action);
            });
            await Pair.RunTicksSync(60);
            await Client.WaitAssertion(() =>
            {
                var windows = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children;
                Assert.That(windows.OfType<TacticalMapWindow>().Any(), Is.EqualTo(classic));
                Assert.That(windows.OfType<CMUReconstructionWindow>().Any(), Is.EqualTo(!classic));
                if (!classic)
                {
                    var view = windows.OfType<CMUReconstructionWindow>().Single().SurveyView;
                    Assert.That(view.Scene, Is.Not.Null);
                    Assert.That(view.Scene!.LoadedChunks, Is.EqualTo(view.Scene.TotalChunks));
                    Assert.That(view.Scene.CanOrder, Is.False, "Personal read access must not grant officer drawing rights.");
                }
            });
            await Server.WaitAssertion(() =>
            {
                Assert.That(_ui.IsUiOpen(marine, TacticalMapUserUi.Key, marine), Is.True);
                _ui.CloseUi(marine, TacticalMapUserUi.Key, marine);
                Assert.That(_recon.BuildSnapshot(marine, marine), Is.Null);
            });
        }
        finally
        {
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, false));
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, original);
                if (marine.IsValid()) SEntMan.DeleteEntity(marine);
            });
            await Pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task ExistingTableSharesSentDrawingsWithClassicCanvasInBothDirections()
    {
        EntityUid table = default;
        var path = new Vector2[] { new(-3.25f, -1.5f), new(2.75f, 4.25f), new(5.5f, 3.75f) };
        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
            var areas = SEntMan.EnsureComponent<AreaGridComponent>(_upper);
#pragma warning disable RA0002
            areas.Colors[new(-8, -8)] = Color.Gray;
            areas.Colors[new(8, 8)] = Color.Gray;
#pragma warning restore RA0002
            table = SEntMan.SpawnEntity("CMUTacticalMapTableGovfor", new EntityCoordinates(_upper, new Vector2(0.5f)));
            SEntMan.RemoveComponent<AccessReaderComponent>(table);
            Assert.That(_ui.TryOpenUi(table, TacticalMapComputerUi.Key, _actor), Is.True);
            void SendTable(BoundUserInterfaceMessage message)
            {
                message.Actor = _actor; message.UiKey = TacticalMapComputerUi.Key;
                SEntMan.EventBus.RaiseLocalEvent(table, (object) message);
            }
            SendTable(new CMUReconViewMessage(Vector2i.Zero));
            var scene = _recon.BuildSnapshot(table, _actor)!;
            Assert.That(scene, Is.Not.Null, "The existing table must serve 3D requests on its existing interface key.");
            SendTable(new CMUReconSendMessage(scene.Generation, 1,
                [new(-1, path, CMUReconInk.Blue, 5), new(0, [new(3, 4)], CMUReconInk.Red, 3, "Hold")], []));
            Assert.That(map.GovforLines, Has.Count.EqualTo(1));
            Assert.That(map.GovforLines[0].WorldPoints, Is.EqualTo(path));
            Assert.That(map.GovforLines[0].Depth, Is.EqualTo(-1));
            Assert.That(map.GovforLines[0].Thickness, Is.EqualTo(5));
            Assert.That(map.GovforLabels[new(3, 4)], Is.EqualTo("Hold"));
            Assert.That(map.OpforLines, Is.Empty, "Faction drawings must not leak.");

            // Classic sends carry the retained 3D stroke alongside a newly drawn canvas-space line.
            var classicLines = new List<TacticalMapLine>(map.GovforLines)
            {
                new(new(18, 30), new(30, 12), Color.Cyan, 4),
            };
            // Classic submission has the normal announcement cooldown; isolate canvas conversion here.
#pragma warning disable RA0002
            SComp<TacticalMapComputerComponent>(table).NextAnnounceAt = TimeSpan.Zero;
#pragma warning restore RA0002
            SendTable(new TacticalMapUpdateCanvasMsg(classicLines, new() { [new(-2, 1)] = "Advance" }));
            var roundTrip = _recon.BuildSnapshot(table, _actor)!.Orders;
            var retained = roundTrip.Single(o => o.Depth == -1);
            Assert.That(retained.Waypoints, Is.EqualTo(path));
            Assert.That(retained.Width, Is.EqualTo(5));
            var added = roundTrip.Single(o => o.Color == Color.Cyan);
            Assert.That(added.Waypoints, Is.EqualTo(new Vector2[] { new(-2, -1), new(2, 5) }), "Canvas origin, scale and the top edge of the highest tile must convert correctly.");
            Assert.That(added.Width, Is.EqualTo(4));
            Assert.That(roundTrip.Single(o => o.Text != null).Text, Is.EqualTo("Advance"));
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            var scene = _recon.BuildSnapshot(table, _actor)!;
            var clear = new CMUReconSendMessage(scene.Generation, 2, [], scene.Orders.Select(o => o.Id).ToArray())
                { Actor = _actor, UiKey = TacticalMapComputerUi.Key };
            SEntMan.EventBus.RaiseLocalEvent(table, (object) clear);
            Assert.That(SComp<TacticalMapComponent>(_upper).GovforLines, Is.Empty);
            Assert.That(SComp<TacticalMapComponent>(_upper).GovforLabels, Is.Empty);
            _ui.CloseUi(table, TacticalMapComputerUi.Key, _actor);
        });
    }
}
