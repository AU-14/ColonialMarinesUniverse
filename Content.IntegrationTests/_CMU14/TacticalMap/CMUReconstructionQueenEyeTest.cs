using Content.Client._RMC14.TacticalMap;
using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.Events;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Watch;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.DoAfter;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Input;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Controlled construction and map fixtures.
    [TestCase(false)]
    [TestCase(true)]
    public async Task QueenEyeMovementDoesNotCancelResinConstruction(bool remote)
    {
        EntityUid queen = default;
        DoAfter pending = null!;
        var target = new EntityCoordinates(_upper, new Vector2(1.5f, 0.5f));
        await Server.WaitAssertion(() =>
        {
            queen = SEntMan.SpawnEntity("CMXenoQueen", new EntityCoordinates(_upper, new Vector2(0.5f)));
            SEntMan.SpawnEntity("XenoWeeds", target);
            SEntMan.SpawnEntity("XenoWeeds", new EntityCoordinates(_upper, new Vector2(6.5f)));
            if (remote) SEntMan.EventBus.RaiseLocalEvent(queen, new QueenEyeActionEvent { Performer = queen });
            var construction = SComp<XenoConstructionComponent>(queen);
            construction.BuildChoice = "WallXenoResin";
            construction.BuildDelay = TimeSpan.FromSeconds(1);
            var action = SEntMan.System<SharedActionsSystem>().GetActions(queen)
                .Single(a => SComp<MetaDataComponent>(a).EntityPrototype?.ID == "ActionXenoSecreteStructure");
            var attempt = new XenoSecreteStructureActionEvent
            {
                Performer = queen, Action = (action, SComp<ActionComponent>(action)), Target = target,
            };
            SEntMan.EventBus.RaiseLocalEvent(queen, attempt);
            Assert.That(attempt.Handled, Is.True);
            pending = SComp<DoAfterComponent>(queen).DoAfters.Values.Single(d => d.Args.Event is XenoSecreteStructureDoAfterEvent);
            var mover = remote ? SComp<QueenEyeActionComponent>(queen).Eye!.Value : queen;
            SEntMan.System<SharedTransformSystem>().SetCoordinates(mover, new EntityCoordinates(_upper, new Vector2(6.5f)));
        });
        await RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(pending.Cancelled, Is.EqualTo(!remote), "Only physical construction should break when its builder moves.");
            var built = _maps.GetAnchoredEntities(_upper, SComp<Robust.Shared.Map.Components.MapGridComponent>(_upper), new Vector2i(1, 0))
                .Any(uid => SEntMan.HasComponent<XenoConstructComponent>(uid));
            Assert.That(built, Is.EqualTo(remote), "The remote order must actually complete at the original tile.");
            SEntMan.DeleteEntity(queen);
        });
    }

    [TestCase(true, false)]
    [TestCase(true, true)]
    [TestCase(false, false)]
    [TestCase(false, true)]
    public async Task QueenEyeTeleportsThroughTheActualMap(bool classic, bool watching)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid queen = default, eye = default, drone = default;
        var destination = new Vector2(6.5f, -4.5f);
        try
        {
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, classic));
            await Server.WaitAssertion(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                var areas = SEntMan.EnsureComponent<AreaGridComponent>(_upper);
                areas.Colors[new(-8, -8)] = Color.Gray;
                areas.Colors[new(8, 8)] = Color.Gray;
                SEntMan.Dirty(_upper, areas);
                queen = SEntMan.SpawnEntity("CMXenoQueen", new EntityCoordinates(_upper, new Vector2(0.5f)));
                SEntMan.SpawnEntity("XenoWeeds", new EntityCoordinates(_upper, new Vector2(0.5f)));
                SEntMan.SpawnEntity("XenoWeeds", new EntityCoordinates(_upper, destination));
                Server.PlayerMan.SetAttachedEntity(session, queen);
                SEntMan.EventBus.RaiseLocalEvent(queen, new QueenEyeActionEvent { Performer = queen });
                eye = SComp<QueenEyeActionComponent>(queen).Eye!.Value;
                if (watching)
                {
                    var hive = SEntMan.SpawnEntity("CMXenoHive", new EntityCoordinates(_upper, new Vector2(-5.5f)));
                    drone = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(_upper, new Vector2(-5.5f)));
                    var hives = SEntMan.System<SharedXenoHiveSystem>();
                    hives.SetHive(queen, hive);
                    hives.SetHive(drone, hive);
                    SEntMan.System<SharedXenoWatchSystem>().Watch(queen, drone);
                    Assert.That(SComp<EyeComponent>(queen).Target, Is.EqualTo(drone));
                }
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(queen, new OpenTacticalMapActionEvent { Performer = queen }));
            await Pair.RunTicksSync(60);
            await Client.WaitAssertion(() =>
            {
                var root = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot;
                if (classic)
                {
                    var map = root.Children.OfType<TacticalMapWindow>().Single().Wrapper.Map;
                    map.Measure(new Vector2(600, 400));
                    map.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(600, 400)));
                    Assert.That(map.QueenEyeMode, Is.True);
                    Assert.That(map.CenterOnPosition(new Vector2i(6, -5)), Is.True);
                    Assert.That(map.PositionToIndices(map.Size / 2), Is.EqualTo(new Vector2i(6, -5)));
                    Click(map, map.Size / 2);
                }
                else
                {
                    var map = root.Children.OfType<CMUReconstructionWindow>().Single().SurveyView;
                    map.Measure(new Vector2(600, 400));
                    map.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(600, 400)));
                    map.RestoreCamera(map.CaptureCamera() with { Center = destination, Distance = 30, Overhead = true, Fit = false });
                    Click(map, map.Size / 2);
                }
            });
            await Pair.RunTicksSync(20);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Vector2.Distance(SEntMan.System<SharedTransformSystem>().GetWorldPosition(eye), destination), Is.LessThan(0.01f));
                Assert.That(SComp<EyeComponent>(queen).Target, Is.EqualTo(eye), "Teleport must return the camera from xeno watch to the eye.");
                Assert.That(SComp<TransformComponent>(queen).LocalPosition, Is.EqualTo(new Vector2(0.5f)), "The queen's body must remain in place.");
            });
        }
        finally
        {
            if (Server.IsAlive) await Server.WaitPost(() =>
            {
                if (queen.IsValid()) _ui.CloseUi(queen, TacticalMapUserUi.Key, queen);
                Server.PlayerMan.SetAttachedEntity(session, original);
                if (queen.IsValid()) SEntMan.DeleteEntity(queen);
                if (drone.IsValid()) SEntMan.DeleteEntity(drone);
            });
            if (Client.IsAlive) await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, false));
            if (Server.IsAlive && Client.IsAlive) await Pair.RunUntilSynced();
        }
    }
    [Test]
    public async Task QueenEyeTeleportRejectsInvalidRequests()
    {
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
            var queen = SEntMan.SpawnEntity("CMXenoQueen", new EntityCoordinates(_upper, new Vector2(0.5f)));
            SEntMan.SpawnEntity("XenoWeeds", new EntityCoordinates(_upper, new Vector2(0.5f)));
            SEntMan.SpawnEntity("XenoWeeds", new EntityCoordinates(_upper, new Vector2(6.5f)));
            SEntMan.SpawnEntity("XenoWeeds", new EntityCoordinates(_lower, new Vector2(6.5f)));
            SEntMan.EventBus.RaiseLocalEvent(queen, new QueenEyeActionEvent { Performer = queen });
            var eye = SComp<QueenEyeActionComponent>(queen).Eye!.Value;
            Assert.That(_ui.TryOpenUi(queen, TacticalMapUserUi.Key, queen), Is.True);
            SComp<TacticalMapUserComponent>(queen).Map = _upper;
            void Request(BoundUserInterfaceMessage message)
            {
                message.Actor = queen;
                message.UiKey = TacticalMapUserUi.Key;
                SEntMan.EventBus.RaiseLocalEvent(queen, (object) message);
            }
            Request(new CMUReconViewMessage(Vector2i.Zero));
            var scene = _recon.BuildSnapshot(queen, queen)!;
            Assert.That(scene, Is.Not.Null);
            Request(new CMUReconQueenEyeMoveMessage(scene.Generation - 1, 0, new Vector2(6.5f)));
            Request(new CMUReconQueenEyeMoveMessage(scene.Generation, int.MinValue, new Vector2(6.5f)));
            Request(new CMUReconQueenEyeMoveMessage(scene.Generation, -1, new Vector2(6.5f)));
            Request(new CMUReconQueenEyeMoveMessage(scene.Generation, 0, new Vector2(float.NaN)));
            Request(new CMUReconQueenEyeMoveMessage(scene.Generation, 0, new Vector2(2000)));
            Request(new CMUReconQueenEyeMoveMessage(scene.Generation, 0, new Vector2(3.5f))); // no weeds
            Request(new TacticalMapQueenEyeMoveMsg(new Vector2i(3, 3))); // no weeds
            var transform = SEntMan.System<SharedTransformSystem>();
            Assert.That(transform.GetWorldPosition(eye), Is.EqualTo(new Vector2(0.5f)));
            Assert.That(transform.GetMapId(eye), Is.EqualTo(transform.GetMapId(queen)));
            Assert.That(SEntMan.System<QueenEyeSystem>().TryTeleport(_actor, new EntityCoordinates(_upper, new Vector2(6.5f))), Is.False);
            _ui.CloseUi(queen, TacticalMapUserUi.Key, queen);
            Request(new CMUReconQueenEyeMoveMessage(scene.Generation, 0, new Vector2(6.5f)));
            Request(new TacticalMapQueenEyeMoveMsg(new Vector2i(6, 6)));
            Assert.That(transform.GetWorldPosition(eye), Is.EqualTo(new Vector2(0.5f)), "Closed maps must not move the eye.");
            // Delete the xeno before the map's leaf-first cleanup deletes its action entities.
            SEntMan.DeleteEntity(queen);
        });
    }

    [Test]
    public async Task ClassicQueenEyeUsesTheDisplayedMap()
    {
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<TacticalMapComponent>(_lower);
            SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
            Assert.That(SEntMan.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().TryGetTacticalMap(out var first), Is.True);
            var displayed = first.Owner == _upper ? _lower : _upper;
            var queen = SEntMan.SpawnEntity("CMXenoQueen", new EntityCoordinates(displayed, new Vector2(0.5f)));
            SEntMan.SpawnEntity("XenoWeeds", new EntityCoordinates(displayed, new Vector2(0.5f)));
            SEntMan.SpawnEntity("XenoWeeds", new EntityCoordinates(displayed, new Vector2(6.5f)));
            SEntMan.EventBus.RaiseLocalEvent(queen, new QueenEyeActionEvent { Performer = queen });
            var eye = SComp<QueenEyeActionComponent>(queen).Eye!.Value;
            Assert.That(_ui.TryOpenUi(queen, TacticalMapUserUi.Key, queen), Is.True);
            SComp<TacticalMapUserComponent>(queen).Map = displayed;
            SEntMan.EventBus.RaiseLocalEvent(queen, (object) new TacticalMapQueenEyeMoveMsg(new Vector2i(6, 6))
                { Actor = queen, UiKey = TacticalMapUserUi.Key });
            Assert.That(SEntMan.System<SharedTransformSystem>().GetWorldPosition(eye), Is.EqualTo(new Vector2(6.5f)));
            _ui.CloseUi(queen, TacticalMapUserUi.Key, queen);
            SEntMan.DeleteEntity(queen);
        });
    }

    [Test]
    public async Task QueenEyeClicksPreservePanAndDrawingTools()
    {
        CMUReconSnapshotMessage snapshot = null!;
        await Server.WaitPost(() => snapshot = _recon.BuildSnapshot(_console, _actor)!);
        await Client.WaitAssertion(() =>
        {
            using var window = new CMUReconstructionWindow();
            window.OpenCentered();
            window.Receive(snapshot);
            var view = window.SurveyView;
            view.Measure(new Vector2(600, 400));
            view.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(600, 400)));
            view.RestoreCamera(view.CaptureCamera() with { Center = new Vector2(3.5f), Distance = 30, Overhead = true, Fit = false });
            view.QueenEyeActive = () => true;
            var clicks = 0;
            view.OnQueenEyeMove += (point, depth) =>
            {
                clicks++;
                Assert.That(point.Floored(), Is.EqualTo(new Vector2i(3, 3)), "Teleporting resolves the selected floor tile.");
                Assert.That(depth, Is.EqualTo(-1));
            };
            view.SelectLevel(0);
            var point = view.Size / 2;
            Click(view, point);
            Assert.That(clicks, Is.EqualTo(1));
            Dispatch(view, "KeyBindDown", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down, default, true, point, point));
            var to = point + new Vector2(30);
            Dispatch(view, "MouseMove", new GUIMouseMoveEventArgs(to - point, view, to, default, to, to));
            Dispatch(view, "KeyBindUp", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up, default, true, to, to));
            view.DrawingEnabled = true;
            Click(view, point);
            view.DrawingEnabled = false;
            view.TextEnabled = true;
            Click(view, point);
            view.TextEnabled = false;
            view.QueenEyeActive = () => false;
            Click(view, point);
            Assert.That(clicks, Is.EqualTo(1), "Panning, drawing, text and leaving eye mode must not teleport.");
        });
    }
#pragma warning restore RA0002
}
