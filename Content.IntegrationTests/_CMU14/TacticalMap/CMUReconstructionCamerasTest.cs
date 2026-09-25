using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Client.Viewport;
using Content.Server.Atmos.Components;
using Content.Shared.Access.Components;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.Inventory;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Build authorized and anonymous intel feeds independently of the viewer.
    [TestCase("close")]
    [TestCase("helmet")]
    [TestCase("skill")]
    [TestCase("faction")]
    [TestCase("map")]
    [TestCase("damage")]
    public async Task MarineIconOpensOverwatchCameraAndReleasesIt(string revoke)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid console = default, squad = default, marine = default, helmet = default, enemy = default;
        NetEntity marineNet = default, helmetNet = default, actorNet = default;
        CMUReconstructionWindow window = null!;
        CMUReconHandshakeTestSystem serverProbe = null!, clientProbe = null!;
        NetEntity consoleNet = default;
        byte[] geometry = null!;
        var blip = new TacticalMapBlip { Indices = new(3, 3), Color = Color.Green };
        try
        {
            await Server.WaitAssertion(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                // The fixture has no atmosphere; pressure damage would correctly interrupt overwatch.
                SEntMan.RemoveComponent<BarotraumaComponent>(_actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                console = SEntMan.SpawnEntity("RMCOverwatchConsoleGovfor", new EntityCoordinates(_upper, new Vector2(1.5f)));
                SEntMan.RemoveComponent<AccessReaderComponent>(console);
                squad = SEntMan.SpawnEntity("SquadGovforBravo", MapCoordinates.Nullspace);
                marine = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(3.5f)));
                enemy = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(6.5f)));
                SEntMan.System<SquadSystem>().AssignSquad(marine, squad, null);
                SEntMan.System<SquadSystem>().AssignSquad(enemy, squad, null);
                SEntMan.System<MetaDataSystem>().SetEntityName(marine, "Ada Marine");
                SEntMan.System<MetaDataSystem>().SetEntityName(enemy, "Secret Sensor Name");
                helmet = SEntMan.SpawnEntity("ArmorHelmetM10", new EntityCoordinates(_upper, new Vector2(3.5f)));
                Assert.That(SEntMan.System<InventorySystem>().TryEquip(marine, helmet, "head", force: true), Is.True);
                var enemyHelmet = SEntMan.SpawnEntity("ArmorHelmetM10", new EntityCoordinates(_upper, new Vector2(6.5f)));
                Assert.That(SEntMan.System<InventorySystem>().TryEquip(enemy, enemyHelmet, "head", force: true), Is.True);
                SEntMan.System<SkillsSystem>().SetSkill(_actor, "RMCSkillOverwatch", 1);
                Server.PlayerMan.SetAttachedEntity(session, _actor);
                marineNet = SEntMan.GetNetEntity(marine);
                helmetNet = SEntMan.GetNetEntity(helmet);
                actorNet = SEntMan.GetNetEntity(_actor);
                consoleNet = SEntMan.GetNetEntity(console);
                serverProbe = SEntMan.System<CMUReconHandshakeTestSystem>();
                serverProbe.Target = console;
                serverProbe.CameraRequests = 0;
            });
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() => Assert.That(_ui.TryOpenUi(console, TacticalMapComputerUi.Key, _actor), Is.True));
            await Pair.RunTicksSync(40);
            await Server.WaitAssertion(() =>
            {
                var computer = SComp<TacticalMapComputerComponent>(console);
                computer.NextUpdate = TimeSpan.MaxValue;
                computer.SquadBlips.Clear();
                computer.Blips = new()
                {
                    [marine.Id] = blip,
                    [enemy.Id] = blip with { Indices = new(6, 6),
                        Image = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "enemy_blip") },
                };
            });
            await Pair.RunTicksSync(20);
            await Server.WaitAssertion(() =>
            {
                var generation = _recon.BuildSnapshot(console, _actor)!.Generation;
                void Request(EntityUid target, int gen) => SEntMan.EventBus.RaiseLocalEvent(console,
                    (object) new CMUReconCameraMessage(gen, SEntMan.GetNetEntity(target))
                        { Actor = _actor, UiKey = TacticalMapComputerUi.Key });
                Request(marine, generation - 1);
                Request(enemy, generation); // Known equipment, but only anonymous sensor intel.
                SEntMan.System<SkillsSystem>().SetSkill(_actor, "RMCSkillOverwatch", 0);
                Request(marine, generation);
                SEntMan.System<SkillsSystem>().SetSkill(_actor, "RMCSkillOverwatch", 1);
                Assert.That(SEntMan.TryGetComponent(_actor, out OverwatchWatchingComponent? watching) && watching.Watching != null, Is.False,
                    "Stale, anonymous and untrained requests must not start a camera subscription.");
            });
            await Client.WaitAssertion(() =>
            {
                window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                clientProbe = CEntMan.System<CMUReconHandshakeTestSystem>();
                clientProbe.Target = CEntMan.GetEntity(consoleNet);
                clientProbe.CameraViews = 0;
                clientProbe.Chunks = 0;
                Assert.That(window.IsRefreshing, Is.False);
                var view = window.SurveyView;
                geometry = view.Scene!.Cells;
                Assert.That(view.TrackedContacts.Single(c => c.Blip.Indices == new Vector2i(3, 3)).Name, Is.EqualTo("Ada Marine"));
                var sensor = view.TrackedContacts.Single(c => c.Blip.Indices == new Vector2i(6, 6));
                Assert.That(sensor.Name, Is.Null, "Enemy sensor intel must not disclose identity.");
                Assert.That(sensor.CameraTarget, Is.Null);
                Assert.That(view.TrackedContacts.Single(c => c.Name == "Ada Marine").CameraTarget, Is.EqualTo(marineNet));
                view.Measure(new Vector2(600, 400));
                view.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(600, 400)));
                view.RestoreCamera(view.CaptureCamera() with { Center = new Vector2(3.5f), Distance = 30, Overhead = true, Fit = false });
                Assert.That(view.CameraAt(view.Size / 2), Is.EqualTo(marineNet), "Hit testing must agree with the visible icon.");
                var requests = 0;
                view.OnCameraRequested += _ => requests++;
                Click(view, view.Size / 2);
                Assert.That(requests, Is.EqualTo(1), "A click must reach the actual BUI handler.");
            });
            await Pair.RunTicksSync(20);
            await Server.WaitAssertion(() =>
            {
                Assert.That(serverProbe.CameraRequests, Is.EqualTo(1), "The server must receive the BUI camera request.");
                Assert.That(_recon.BuildSnapshot(console, _actor), Is.Not.Null, "Camera viewing must keep the survey open.");
                Assert.That(_ui.IsUiOpen(console, TacticalMapComputerUi.Key, _actor), Is.True);
                Assert.That(SEntMan.TryGetComponent(_actor, out OverwatchWatchingComponent? watching) ? watching.Watching : null, Is.EqualTo(helmet));
                Assert.That(SComp<EyeComponent>(_actor).Target, Is.EqualTo(helmet));
                Assert.That(SComp<OverwatchCameraComponent>(helmet).Watching, Does.Contain(_actor));
            });
            await Client.WaitAssertion(() =>
            {
                var camera = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconCameraWindow>().Single();
                Dispatch(camera, "FrameUpdate", new Robust.Shared.Timing.FrameEventArgs(0));
                Assert.That(camera.Title, Does.Contain("Ada Marine"));
                var viewport = camera.FindControl<ScalingViewport>("CameraViewport");
                Assert.That(viewport.Visible, Is.True);
                var actor = CEntMan.GetEntity(actorNet);
                Assert.That(viewport.Eye, Is.SameAs(CEntMan.GetComponent<EyeComponent>(actor).Eye));
                Assert.That(CEntMan.GetNetEntity(CEntMan.GetComponent<OverwatchWatchingComponent>(actor).Watching!.Value), Is.EqualTo(helmetNet));
                Assert.That(window.IsOpen, Is.True, "The tactical map must remain available beside the camera.");
                Assert.That(window.SurveyView.Scene!.Cells, Is.SameAs(geometry));
                Assert.That(clientProbe.Chunks, Is.Zero, "A helmet camera must not reload the terrain.");
                Assert.That(clientProbe.CameraViews, Is.EqualTo(1));
                if (revoke == "close") camera.Close();
            });
            if (revoke != "close")
            {
                await Server.WaitAssertion(() =>
                {
                    if (revoke == "helmet")
                        Assert.That(SEntMan.System<InventorySystem>().TryUnequip(marine, "head", force: true), Is.True);
                    else if (revoke == "skill")
                        SEntMan.System<SkillsSystem>().SetSkill(_actor, "RMCSkillOverwatch", 0);
                    else if (revoke == "map")
                        _ui.CloseUi(console, TacticalMapComputerUi.Key, _actor);
                    else if (revoke == "damage")
                        SEntMan.System<DamageableSystem>().TryChangeDamage(_actor,
                            new DamageSpecifier { DamageDict = new() { ["Blunt"] = 1 } }, ignoreResistances: true);
                    else
                        SEntMan.System<SharedOverwatchConsoleSystem>().SetGroup((console, SComp<OverwatchConsoleComponent>(console)), "OPFOR");
                });
            }
            await Pair.RunTicksSync(25);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.TryGetComponent(_actor, out OverwatchWatchingComponent? watching) && watching.Watching != null, Is.False);
                Assert.That(SComp<EyeComponent>(_actor).Target, Is.Null);
                Assert.That(SComp<OverwatchCameraComponent>(helmet).Watching, Does.Not.Contain(_actor));
            });
            await Client.WaitAssertion(() =>
                Assert.That(Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconCameraWindow>(), Is.Empty));
        }
        finally
        {
            if (Client.IsAlive)
                await Client.WaitPost(() => { if (clientProbe != null) clientProbe.Target = default; });
            await Server.WaitPost(() =>
            {
                if (serverProbe != null) serverProbe.Target = default;
                if (console.IsValid()) _ui.CloseUi(console, TacticalMapComputerUi.Key, _actor);
                Server.PlayerMan.SetAttachedEntity(session, original);
                foreach (var uid in new[] { marine, helmet, enemy, console, squad })
                    if (uid.IsValid() && SEntMan.EntityExists(uid)) SEntMan.DeleteEntity(uid);
            });
            if (Client.IsAlive && Server.IsAlive)
                await Pair.RunUntilSynced();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PersonalNamesRemainReadOnlyEvenForTrainedViewers(bool ghost)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid viewer = default, marine = default, squad = default;
        NetEntity marineNet = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                viewer = SEntMan.SpawnEntity(ghost ? "MobObserver" : "CMMobHuman", new EntityCoordinates(_upper, new Vector2(1.5f)));
                marine = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(3.5f)));
                squad = SEntMan.SpawnEntity("SquadGovforBravo", MapCoordinates.Nullspace);
                SEntMan.System<SquadSystem>().AssignSquad(marine, squad, null);
                SEntMan.System<MetaDataSystem>().SetEntityName(marine, "Friendly Marine");
                var helmet = SEntMan.SpawnEntity("ArmorHelmetM10", new EntityCoordinates(_upper, new Vector2(3.5f)));
                Assert.That(SEntMan.System<InventorySystem>().TryEquip(marine, helmet, "head", force: true), Is.True);
                SEntMan.System<SkillsSystem>().SetSkill(viewer, "RMCSkillOverwatch", 1);
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                marineNet = SEntMan.GetNetEntity(marine);
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(viewer, new OpenTacticalMapActionEvent { Performer = viewer }));
            await Pair.RunTicksSync(40);
            await Server.WaitPost(() =>
            {
                var user = SComp<TacticalMapUserComponent>(viewer);
                user.Marines = true;
                user.MarineBlips = new() { [marine.Id] = new TacticalMapBlip { Indices = new(3, 3), Color = Color.Green } };
            });
            await Pair.RunTicksSync(20);
            await Client.WaitAssertion(() =>
            {
                var view = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView;
                var contact = view.TrackedContacts.Single(c => c.Name == "Friendly Marine");
                Assert.That(contact.CameraTarget, Is.Null, "Personal maps, including ghost maps, cannot grant overwatch camera access.");
                view.OnCameraRequested!.Invoke(marineNet); // Simulate a forged request through the actual BUI.
            });
            await Pair.RunTicksSync(20);
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.TryGetComponent(viewer, out OverwatchWatchingComponent? watching) && watching.Watching != null, Is.False));
            await Client.WaitAssertion(() =>
                Assert.That(Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconCameraWindow>(), Is.Empty));
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (viewer.IsValid()) _ui.CloseUi(viewer, TacticalMapUserUi.Key, viewer);
                Server.PlayerMan.SetAttachedEntity(session, original);
                foreach (var uid in new[] { viewer, marine, squad })
                    if (uid.IsValid() && SEntMan.EntityExists(uid)) SEntMan.DeleteEntity(uid);
            });
            await Pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task CameraClicksDoNotOverridePanDrawingOrHiddenFloors()
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
            var target = new NetEntity(123);
            view.TrackedContacts = [new(0, new TacticalMapBlip { Indices = new(3, 3) }, "Test Marine", target)];
            var clicks = 0;
            view.OnCameraRequested += _ => clicks++;
            var point = view.Size / 2;
            Click(view, point);
            Assert.That(clicks, Is.EqualTo(1));
            Dispatch(view, "KeyBindDown", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down, default, true, point, point));
            var to = point + new Vector2(30);
            Dispatch(view, "MouseMove", new GUIMouseMoveEventArgs(to - point, view, to, default, to, to));
            Dispatch(view, "KeyBindUp", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up, default, true, to, to));
            Assert.That(clicks, Is.EqualTo(1), "Dragging from a marine icon must pan without opening a camera.");
            view.RestoreCamera(view.CaptureCamera() with { Center = new Vector2(3.5f), Distance = 30, Overhead = true, Fit = false });
            view.DrawingEnabled = true;
            Click(view, point);
            view.DrawingEnabled = false;
            view.TextEnabled = true;
            Click(view, point);
            view.TextEnabled = false;
            Assert.That(clicks, Is.EqualTo(1), "Pencil and text tools must retain their clicks.");
            view.ShowContacts = false;
            Assert.That(view.CameraAt(point), Is.Null);
            view.ShowContacts = true;
            view.SelectLevel(0);
            Assert.That(view.CameraAt(point), Is.Null, "Contacts on another floor must not be clickable.");
        });
    }
#pragma warning restore RA0002
}
