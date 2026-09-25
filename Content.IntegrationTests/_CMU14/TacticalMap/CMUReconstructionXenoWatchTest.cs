using Content.Client._RMC14.TacticalMap;
using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Server._RMC14.Xenonids.Watch;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Watch;
using Content.Shared.CCVar;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Build deterministic authorized tactical feeds and hive fixtures.
    [TestCase(true, false)]
    [TestCase(true, true)]
    [TestCase(false, false)]
    [TestCase(false, true)]
    public async Task QueenWatchesXenoIconsThroughBothMaps(bool classic, bool remoteEye)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid queen = default, first = default, second = default, eye = default;
        var firstTile = new Vector2i(3, 3);
        var secondTile = new Vector2i(-4, 5);
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
                first = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(_upper, (Vector2) firstTile + new Vector2(0.5f)));
                second = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(_upper, (Vector2) secondTile + new Vector2(0.5f)));
                var hive = SEntMan.SpawnEntity("CMXenoHive", new EntityCoordinates(_upper, Vector2.Zero));
                foreach (var xeno in new[] { queen, first, second })
                {
                    SEntMan.System<SharedXenoHiveSystem>().SetHive(xeno, hive);
                    SEntMan.SpawnEntity("XenoWeeds", SComp<TransformComponent>(xeno).Coordinates);
                }
                Server.PlayerMan.SetAttachedEntity(session, queen);
                if (remoteEye)
                {
                    SEntMan.EventBus.RaiseLocalEvent(queen, new QueenEyeActionEvent { Performer = queen });
                    eye = SComp<QueenEyeActionComponent>(queen).Eye!.Value;
                }
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(queen, new OpenTacticalMapActionEvent { Performer = queen }));
            await Pair.RunTicksSync(60);
            await Server.WaitAssertion(() =>
            {
                SComp<TacticalMapComponent>(_upper).NextUpdatePerFaction[SharedTacticalMapSystem.XenosFaction] = TimeSpan.MaxValue;
                var user = SComp<TacticalMapUserComponent>(queen);
                user.XenoBlips = new()
                {
                    [first.Id] = new TacticalMapBlip { Indices = firstTile, Color = Color.Green },
                    [second.Id] = new TacticalMapBlip { Indices = secondTile, Color = Color.Green },
                };
                SEntMan.Dirty(queen, user);
            });
            await Pair.RunTicksSync(20);

            async Task Select(Vector2i tile, EntityUid target)
            {
                var net = SEntMan.GetNetEntity(target);
                await Client.WaitAssertion(() =>
                {
                    var root = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot;
                    if (classic)
                    {
                        var view = root.Children.OfType<TacticalMapWindow>().Single().Wrapper.Map;
                        view.Measure(new Vector2(600, 400));
                        view.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(600, 400)));
                        Assert.That(view.CenterOnPosition(tile), Is.True);
                        Assert.That(view.CanQueenWatchBlip!(target.Id), Is.True);
                        Click(view, view.Size / 2);
                    }
                    else
                    {
                        var view = root.Children.OfType<CMUReconstructionWindow>().Single().SurveyView;
                        view.Measure(new Vector2(600, 400));
                        view.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(600, 400)));
                        view.RestoreCamera(view.CaptureCamera() with
                            { Center = (Vector2) tile + new Vector2(0.5f), Distance = 30, Overhead = true, Fit = false });
                        Assert.That(view.XenoAt(view.Size / 2), Is.EqualTo(net));
                        Assert.That(view.CameraAt(view.Size / 2), Is.Null, "Xeno watch must not grant helmet-camera access.");
                        Click(view, view.Size / 2);
                    }
                });
                await Pair.RunTicksSync(20);
                await Server.WaitAssertion(() =>
                {
                    Assert.That(SComp<EyeComponent>(queen).Target, Is.EqualTo(target));
                    Assert.That(SComp<XenoWatchingComponent>(queen).Watching, Is.EqualTo(target));
                    Assert.That(session.ViewSubscriptions, Does.Contain(target));
                    Assert.That(SComp<TransformComponent>(queen).LocalPosition, Is.EqualTo(new Vector2(0.5f)));
                    if (remoteEye)
                        Assert.That(SComp<TransformComponent>(eye).LocalPosition, Is.EqualTo(new Vector2(0.5f)),
                            "Clicking a xeno on weeds must watch it instead of teleporting the eye.");
                });
            }

            await Select(firstTile, first);
            await Select(secondTile, second);
            await Server.WaitAssertion(() =>
            {
                Assert.That(session.ViewSubscriptions, Does.Not.Contain(first), "Switching must release the previous watched view.");
                var mover = remoteEye ? eye : queen;
                var input = SComp<InputMoverComponent>(mover);
                input.HeldMoveButtons = MoveButtons.Up;
                var movement = new MoveInputEvent((mover, input), MoveButtons.None);
                SEntMan.EventBus.RaiseLocalEvent(mover, ref movement);
                input.HeldMoveButtons = MoveButtons.None;
                Assert.That(SComp<EyeComponent>(queen).Target, Is.EqualTo(remoteEye ? (EntityUid?) eye : null));
            });
            await Pair.RunTicksSync(10);
            await Select(secondTile, second); // Rewatch after movement, even if the same icon was last selected.
            await Server.WaitAssertion(() =>
            {
                _ui.CloseUi(queen, TacticalMapUserUi.Key, queen);
                Assert.That(SComp<EyeComponent>(queen).Target, Is.EqualTo(second), "Closing the map should reveal the watched xeno.");
            });
        }
        finally
        {
            if (Server.IsAlive) await Server.WaitPost(() =>
            {
                if (queen.IsValid()) _ui.CloseUi(queen, TacticalMapUserUi.Key, queen);
                Server.PlayerMan.SetAttachedEntity(session, original);
                foreach (var xeno in new[] { queen, first, second })
                    if (xeno.IsValid() && SEntMan.EntityExists(xeno)) SEntMan.DeleteEntity(xeno);
            });
            if (Client.IsAlive) await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, false));
            if (Server.IsAlive && Client.IsAlive) await Pair.RunUntilSynced();
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task QueenXenoWatchRejectsUnauthorizedMapRequests(bool classic)
    {
        await Server.WaitAssertion(() =>
        {
            var session = ServerSession!;
            var original = session.AttachedEntity;
            var queen = SEntMan.SpawnEntity("CMXenoQueen", new EntityCoordinates(_upper, new Vector2(0.5f)));
            var friend = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(_upper, new Vector2(3.5f)));
            var enemy = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(_upper, new Vector2(6.5f)));
            try
            {
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                var hive = SEntMan.SpawnEntity("CMXenoHive", new EntityCoordinates(_upper, Vector2.Zero));
                var otherHive = SEntMan.SpawnEntity("CMXenoHive", new EntityCoordinates(_upper, Vector2.Zero));
                var hives = SEntMan.System<SharedXenoHiveSystem>();
                hives.SetHive(queen, hive);
                hives.SetHive(friend, hive);
                hives.SetHive(enemy, otherHive);
                Server.PlayerMan.SetAttachedEntity(session, queen);
                Assert.That(_ui.TryOpenUi(queen, TacticalMapUserUi.Key, queen), Is.True);
                var user = SComp<TacticalMapUserComponent>(queen);
                user.Map = _upper;
                user.Xenos = true;
                user.XenoBlips.Clear();
                SEntMan.EventBus.RaiseLocalEvent(queen, (object) new CMUReconViewMessage(Vector2i.Zero)
                    { Actor = queen, UiKey = TacticalMapUserUi.Key });
                var generation = _recon.BuildSnapshot(queen, queen)!.Generation;
                void Request(EntityUid target, int? gen = null)
                {
                    BoundUserInterfaceMessage message = classic ? new TacticalMapQueenWatchMsg(target.Id) :
                        new CMUReconXenoWatchMessage(gen ?? generation, SEntMan.GetNetEntity(target));
                    message.Actor = queen;
                    message.UiKey = TacticalMapUserUi.Key;
                    SEntMan.EventBus.RaiseLocalEvent(queen, (object) message);
                    Assert.That(SComp<EyeComponent>(queen).Target, Is.Null, "Unauthorized requests must not start watching.");
                }
                var blip = new TacticalMapBlip { Indices = new(3, 3), Color = Color.Green };
                Request(friend); // Not in the authorized feed.
                user.XenoBlips[friend.Id] = blip;
                user.XenoBlips[enemy.Id] = blip;
                user.XenoBlips[_console.Id] = blip;
                user.XenoBlips[queen.Id] = blip;
                Request(enemy);
                Request(_console);
                Request(queen);
                if (!classic) Request(friend, generation - 1);
                user.XenoBlips[friend.Id] = blip with
                    { Image = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "enemy_blip") };
                Request(friend);
                user.XenoBlips[friend.Id] = blip;
                SEntMan.System<MobStateSystem>().ChangeMobState(friend, MobState.Dead);
                Request(friend);
                SEntMan.System<MobStateSystem>().ChangeMobState(friend, MobState.Alive);
                var watch = SEntMan.System<XenoWatchSystem>();
                Assert.That(watch.CanQueenWatch(friend, queen), Is.False, "An ordinary xeno cannot gain queen-map watch access.");
                Assert.That(watch.CanQueenWatch(queen, friend), Is.True);
                _ui.CloseUi(queen, TacticalMapUserUi.Key, queen);
                Request(friend);
            }
            finally
            {
                _ui.CloseUi(queen, TacticalMapUserUi.Key, queen);
                Server.PlayerMan.SetAttachedEntity(session, original);
                foreach (var xeno in new[] { queen, friend, enemy }) SEntMan.DeleteEntity(xeno);
            }
        });
    }

    [Test]
    public async Task QueenXenoWatchClicksPreserveDrawingAndPanning()
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
            var camera = view.CaptureCamera() with { Center = new Vector2(3.5f), Distance = 30, Overhead = true, Fit = false };
            view.RestoreCamera(camera);
            view.QueenEyeActive = () => true;
            var target = new NetEntity(123);
            view.TrackedContacts = [new(0, new TacticalMapBlip { Indices = new(3, 3) }, XenoWatchTarget: target)];
            var watches = 0;
            var teleports = 0;
            view.OnXenoWatchRequested += _ => watches++;
            view.OnQueenEyeMove += (_, _) => teleports++;
            var point = view.Size / 2;
            Click(view, point);
            Assert.That(watches, Is.EqualTo(1));
            Dispatch(view, "KeyBindDown", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down, default, true, point, point));
            var to = point + new Vector2(30);
            Dispatch(view, "MouseMove", new GUIMouseMoveEventArgs(to - point, view, to, default, to, to));
            Dispatch(view, "KeyBindUp", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up, default, true, to, to));
            view.RestoreCamera(camera);
            view.DrawingEnabled = true;
            Click(view, point);
            view.DrawingEnabled = false;
            view.TextEnabled = true;
            Click(view, point);
            view.TextEnabled = false;
            Dispatch(view, "KeyBindDown", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down, default, true, point, point));
            view.TrackedContacts = [];
            Dispatch(view, "KeyBindUp", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up, default, true, point, point));
            Assert.That(watches, Is.EqualTo(1));
            Assert.That(teleports, Is.Zero, "Xeno gestures must not turn into terrain teleports.");
            view.TrackedContacts = [new(0, new TacticalMapBlip { Indices = new(3, 3) }, XenoWatchTarget: target)];
            view.ShowContacts = false;
            Assert.That(view.XenoAt(point), Is.Null);
            view.ShowContacts = true;
            view.SelectLevel(0);
            Assert.That(view.XenoAt(point), Is.Null);
        });
    }
#pragma warning restore RA0002
}
