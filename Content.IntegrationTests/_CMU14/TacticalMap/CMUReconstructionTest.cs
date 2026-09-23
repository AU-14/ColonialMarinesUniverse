using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.TacticalMap.Reconstruction;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input;
using System.Reflection;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.TacticalMap;

[TestFixture]
public sealed partial class CMUReconstructionTest : GameTest
{
    private EntityUid _lower;
    private EntityUid _upper;
    private EntityUid _network;
    private EntityUid _console;
    private EntityUid _actor;
    private CMUTacticalReconstructionSystem _recon = null!;
    private SharedMapSystem _maps = null!;
    private SharedUserInterfaceSystem _ui = null!;
    private const CMUReconstructionUiKey Key = CMUReconstructionUiKey.Key;

    [SetUp]
    public async Task SetupSurvey()
    {
        await Server.WaitAssertion(() =>
        {
            _recon = SEntMan.System<CMUTacticalReconstructionSystem>();
            _maps = SEntMan.System<SharedMapSystem>();
            _ui = SEntMan.System<SharedUserInterfaceSystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            _lower = _maps.CreateMap(runMapInit: true);
            _upper = _maps.CreateMap(runMapInit: true);
            foreach (var map in new[] { _lower, _upper })
            {
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                for (var x = -8; x <= 8; x++)
                for (var y = -8; y <= 8; y++)
                    _maps.SetTile(map, grid, new Vector2i(x, y), floor);
            }
            var z = SEntMan.System<CMUZLevelsSystem>();
            var network = z.CreateZNetwork();
            _network = network;
            Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [_lower] = -1, [_upper] = 0 }), Is.True);
            _console = SEntMan.SpawnEntity("CMUTacticalReconstructionTableGovfor", new EntityCoordinates(_upper, new Vector2(0.5f)));
            SEntMan.RemoveComponent<AccessReaderComponent>(_console);
            _actor = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(_upper, new Vector2(0.5f, 1.5f)));
            var skill = SEntMan.System<SkillsSystem>();
            skill.SetSkill(_actor, "RMCSkillLeadership", 2);
            Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True);
            Send(new CMUReconViewMessage(Vector2i.Zero));
            Assert.That(_recon.BuildSnapshot(_console, _actor), Is.Not.Null);
        });
    }

    [TearDown]
    public async Task CleanupSurvey()
    {
        await Server.WaitPost(() =>
        {
            _ui.CloseUi(_console, Key, _actor);
            SEntMan.DeleteEntity(_actor);
        });
        foreach (var uid in new[] { _upper, _lower, _network })
            if (uid.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(uid);
    }

    private void Send(BoundUserInterfaceMessage message)
    {
        message.Actor = _actor;
        message.UiKey = Key;
        SEntMan.EventBus.RaiseLocalEvent(_console, (object) message);
    }

    [Test]
    public async Task SurveyKeepsInitialGeometryAfterWorldChangesAndIdleReopen()
    {
        EntityUid wall = default;
        EntityUid door = default;
        CMUReconSnapshotMessage initial = null;
        var tile = new Vector2i(3, 3);
        await Server.WaitAssertion(() =>
        {
            wall = SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(_upper, new Vector2(3.5f)));
            door = SEntMan.SpawnEntity("CMDoubleDoorAlmayerSolid", new EntityCoordinates(_upper, new Vector2(4.5f, 3.5f)));
            Assert.That(_recon.ReadCell(_upper, tile), Is.EqualTo((byte) CMUReconMaterial.Wall));
            Assert.That(_recon.ReadCell(_lower, tile), Is.EqualTo((byte) CMUReconMaterial.Floor));
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            var snapshot = _recon.BuildSnapshot(_console, _actor)!;
            var local = tile - snapshot.Origin;
            Assert.That(snapshot.MinDepth, Is.EqualTo(-1));
            Assert.That(snapshot.Levels, Is.EqualTo(2));
            Assert.That(snapshot.LoadedChunks, Is.EqualTo(snapshot.TotalChunks));
            Assert.That(snapshot.Cells[CMUReconGeometry.Index(local.X, local.Y, 1, snapshot.Width, snapshot.Height)], Is.EqualTo((byte) CMUReconMaterial.Wall));
            initial = snapshot;
            SEntMan.DeleteEntity(wall);
            _maps.SetTile(_upper, SComp<MapGridComponent>(_upper), tile, Tile.Empty);
            _maps.SetTile(_upper, SComp<MapGridComponent>(_upper), new Vector2i(160, -90),
                new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId));
            SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(_upper, new Vector2(5.5f, 3.5f)));
            Assert.That(SEntMan.System<SharedDoorSystem>().SetState(door, DoorState.Open), Is.True);
            Assert.That(_recon.ReadCell(_upper, tile), Is.Zero);
            Assert.That(_recon.ReadCell(_upper, new(5, 3)), Is.EqualTo((byte) CMUReconMaterial.Wall));
            Assert.That(_recon.ReadCell(_upper, new(4, 3)), Is.EqualTo((byte) CMUReconMaterial.OpenDoubleDoor));
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            var snapshot = _recon.BuildSnapshot(_console, _actor)!;
            Assert.That(snapshot.Cells, Is.EqualTo(initial.Cells), "World changes must not update the displayed terrain.");
            Assert.That(snapshot.Appearance, Is.EqualTo(initial.Appearance), "Door textures must remain part of the initial survey.");
            Assert.That(snapshot.Revisions, Is.EqualTo(initial.Revisions));
            _ui.CloseUi(_console, Key, _actor);
        });
        // Exceed the former five-minute server cache lifetime; request without a client cache.
        await RunSeconds(310);
        await Server.WaitAssertion(() =>
        {
            // The unprotected test human can die during the idle interval. A fresh viewer must
            // receive the same server baseline without relying on either viewer's client cache.
            SEntMan.DeleteEntity(_actor);
            _actor = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(_upper, new Vector2(0.5f, 1.5f)));
            SEntMan.System<SkillsSystem>().SetSkill(_actor, "RMCSkillLeadership", 2);
            Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True);
            Send(new CMUReconViewMessage(Vector2i.Zero));
            var reopened = _recon.BuildSnapshot(_console, _actor)!;
            Assert.That(reopened.AtlasId, Is.EqualTo(initial.AtlasId));
            Assert.That((reopened.Origin, reopened.Width, reopened.Height), Is.EqualTo((initial.Origin, initial.Width, initial.Height)),
                "New tiles beyond the original map bounds must not expand the survey on reopening.");
            Assert.That(reopened.Cells, Is.EqualTo(initial.Cells));
            Assert.That(reopened.Appearance, Is.EqualTo(initial.Appearance));
            Assert.That(reopened.Directions, Is.EqualTo(initial.Directions));
        });
    }

    [Test]
    public async Task FrozenSurveySurvivesConstructionAndLinkedFloorChanges()
    {
        EntityUid extra = default;
        CMUReconSnapshotMessage before = null;
        EntityUid wall = default;
        try
        {
            await Server.WaitPost(() => wall = SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(_upper, new Vector2(3.5f))));
            await Pair.RunTicksSync(40);
            await Server.WaitPost(() =>
            {
                before = _recon.BuildSnapshot(_console, _actor)!;
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.DeleteEntity(wall);
                SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(_upper, new Vector2(5.5f)));
                var building = SEntMan.System<Content.Server.CMU14.ZLevelBuilding.ZLevelBuildingSystem>();
                Assert.That(building.EnsureNeighborLevel(_upper, 1, _upper, Vector2.Zero, out extra, out var grid), Is.True);
                _maps.SetTile(grid, SComp<MapGridComponent>(grid), Vector2i.Zero,
                    new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId));
            });
            await Pair.RunTicksSync(40);
            await Server.WaitPost(() =>
            {
                Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True);
                Send(new CMUReconViewMessage(Vector2i.Zero));
            });
            await Pair.RunTicksSync(40);
            await Server.WaitAssertion(() =>
            {
                var after = _recon.BuildSnapshot(_console, _actor)!;
                Assert.That(after.AtlasId, Is.EqualTo(before.AtlasId), "The same surviving map must never take a second structural survey.");
                Assert.That(after.Cells, Is.EqualTo(before.Cells), "Keep destroyed structures and omit newly built walls.");
                Assert.That(after.Levels, Is.EqualTo(before.Levels), "The saved survey includes only its original floors.");
            });
        }
        finally
        {
            if (extra.IsValid()) await Pair.DeleteEntityTreeLeafFirst(extra);
        }
    }

    [Test]
    public async Task OrdersValidateCurrentGeometryGenerationTrainingAndFaction()
    {
        var tile = new Vector2i(4, 4);
        await Server.WaitAssertion(() =>
        {
            var snapshot = _recon.BuildSnapshot(_console, _actor)!;
            Send(new CMUReconOrderMessage(snapshot.Generation + 1, 0, tile, CMUReconOrderKind.Move));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Is.Empty);
            Send(new CMUReconOrderMessage(snapshot.Generation, -1, tile, CMUReconOrderKind.Rally));
            var order = _recon.BuildSnapshot(_console, _actor)!.Orders.Single();
            Assert.That(order.Tile, Is.EqualTo(tile));
            Assert.That(order.Depth, Is.EqualTo(-1));
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            var generation = _recon.BuildSnapshot(_console, _actor)!.Generation;
            SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(_upper, new Vector2(4.5f)));
            Send(new CMUReconOrderMessage(generation, 0, tile, CMUReconOrderKind.Move));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Has.Length.EqualTo(1), "A stale clear-floor preview cannot order into a new wall.");
            SEntMan.System<SkillsSystem>().SetSkill(_actor, "RMCSkillLeadership", 0);
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            var generation = _recon.BuildSnapshot(_console, _actor)!.Generation;
            Send(new CMUReconOrderMessage(generation, -1, new Vector2i(5, 5), CMUReconOrderKind.Move));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Has.Length.EqualTo(1));
            SEntMan.System<SkillsSystem>().SetSkill(_actor, "RMCSkillLeadership", 2);
            var tactical = SComp<TacticalMapComputerComponent>(_console);
            SEntMan.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().SetComputerFaction((_console, tactical), "opfor");
            Send(new CMUReconClearOrdersMessage());
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Has.Length.EqualTo(1),
                "A faction change must reject an old window's clear request before the next update.");
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_recon.BuildSnapshot(_console, _actor), Is.Null, "Changing console faction invalidates the previous survey.");
            Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True);
            Send(new CMUReconViewMessage(Vector2i.Zero));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Is.Empty, "Faction orders must not carry across a faction change.");
        });
    }

    [Test]
    public async Task ConnectedOperatorReceivesSurveyAndSharedOrder()
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        NetEntity console = default;
        await Server.WaitPost(() =>
        {
            _ui.CloseUi(_console, Key, _actor);
            Server.PlayerMan.SetAttachedEntity(session, _actor);
            console = SEntMan.GetNetEntity(_console);
        });
        try
        {
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() => Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True));
            await Pair.RunTicksSync(40);
            CMUReconstructionControl view = null!;
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                    .OfType<CMUReconstructionWindow>().Single();
                view = window.FindControl<CMUReconstructionControl>("View");
                Assert.That(view.Scene, Is.Not.Null, "Opening the BUI must request and receive the structural atlas.");
                Assert.That(view.Scene!.LoadedChunks, Is.EqualTo(view.Scene.TotalChunks), "Every chunk must reach the connected viewer.");
                Assert.That(view.Scene.Appearance.Any(style => (style & 0xffff) != 0), Is.True, "Actual floor styles must cross the wire.");
                var ui = CEntMan.System<SharedUserInterfaceSystem>();
                Assert.That(ui.TryGetOpenUi<CMUReconstructionBui>(CEntMan.GetEntity(console), Key, out var bui), Is.True);
                bui!.SendMessage(new CMUReconOrderMessage(view.Scene!.Generation, -1, new Vector2i(4, 4), CMUReconOrderKind.Rally));
            });
            await Pair.RunTicksSync(40);
            await Server.WaitAssertion(() => Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Has.Length.EqualTo(1)));
            await Client.WaitAssertion(() =>
            {
                Assert.That(view.Orders, Has.Length.EqualTo(1));
                Assert.That(view.Orders[0].Depth, Is.EqualTo(-1));
                Assert.That(view.Orders[0].Tile, Is.EqualTo(new Vector2i(4, 4)));
                var ui = CEntMan.System<SharedUserInterfaceSystem>();
                Assert.That(ui.TryGetOpenUi<CMUReconstructionBui>(CEntMan.GetEntity(console), Key, out var bui), Is.True);
                bui!.SendMessage(new CMUReconRouteMessage(view.Scene!.Generation, -1, [new(2.25f, -4), new(6, -4.75f)], CMUReconInk.Blue));
            });
            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() =>
            {
                var route = view.Orders.Single(o => o.Kind == CMUReconOrderKind.Route);
                Assert.That(route.Waypoints, Is.EqualTo(new Vector2[] { new(2.25f, -4), new(6, -4.75f) }));
                Assert.That(route.Ink, Is.EqualTo(CMUReconInk.Blue));
                var ui = CEntMan.System<SharedUserInterfaceSystem>();
                Assert.That(ui.TryGetOpenUi<CMUReconstructionBui>(CEntMan.GetEntity(console), Key, out var bui), Is.True);
                bui!.SendMessage(new CMUReconCancelOrderMessage(view.Scene!.Generation, route.Id));
            });
            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() => Assert.That(view.Orders, Has.Length.EqualTo(1), "Cancelling a route must retain other faction orders."));
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                Server.PlayerMan.SetAttachedEntity(session, original);
            });
            await Pair.RunUntilSynced();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReopenedSurveyRecoversDroppedHandshake(bool dropReply)
    {
        // This case exercises retained manual camera state; centering has separate coverage.
        await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapCenterOnOpen, false));
        var session = ServerSession!;
        var original = session.AttachedEntity;
        NetEntity console = default;
        CMUReconHandshakeTestSystem serverProbe = null!;
        CMUReconHandshakeTestSystem clientProbe = null!;
        CMUReconstructionWindow window = null!;
        CMUReconstructionControl view = null!;
        CMUReconCamera savedCamera = default;
        await Server.WaitPost(() =>
        {
            _ui.CloseUi(_console, Key, _actor);
            Server.PlayerMan.SetAttachedEntity(session, _actor);
            console = SEntMan.GetNetEntity(_console);
            serverProbe = SEntMan.System<CMUReconHandshakeTestSystem>();
            serverProbe.Target = _console;
            serverProbe.Requests = 0;
        });
        try
        {
            await Pair.RunUntilSynced();
            await Client.WaitPost(() =>
            {
                clientProbe = CEntMan.System<CMUReconHandshakeTestSystem>();
                clientProbe.Target = CEntMan.GetEntity(console);
            });
            await Server.WaitAssertion(() => Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True));
            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() =>
            {
                window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                    .OfType<CMUReconstructionWindow>().Single();
                var initial = window.SurveyView;
                Assert.That(initial.Scene, Is.Not.Null);
                initial.SelectLevel(0);
                initial.Rotate(0.4f);
                initial.Pan(new Vector2(3, -2));
                initial.SetLowWalls(true);
                initial.SetIsolated(true);
                savedCamera = initial.CaptureCamera();
                window.Close();
            });
            await Pair.RunTicksSync(20);

            // Lose the initial request or metadata reply on the next opening. Patches alone
            // cannot initialize a fresh window, even if the server has finished the atlas.
            await Client.WaitPost(() => clientProbe.DropSnapshot = dropReply);
            await Server.WaitAssertion(() =>
            {
                Assert.That(_recon.BuildSnapshot(_console, _actor), Is.Null);
                serverProbe.DropRequest = !dropReply;
                Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True);
            });
            await Pair.RunTicksSync(20);
            await Client.WaitAssertion(() =>
            {
                window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                    .OfType<CMUReconstructionWindow>().Single();
                view = window.FindControl<CMUReconstructionControl>("View");
                Assert.That(view.Scene, Is.Not.Null, "The saved map must be visible before any fresh metadata arrives.");
                Assert.That(window.IsRefreshing, Is.True, "A dropped handshake must not make the cached survey authoritative.");
                Assert.That(view.CaptureCamera(), Is.EqualTo(savedCamera));
                Assert.That(window.FindControl<CheckBox>("Pencil").Disabled, Is.True);
                Assert.That(window.FindControl<Button>("Clear").Disabled, Is.True);
                Assert.That(clientProbe.DropSnapshot, Is.False);
            });
            await Server.WaitAssertion(() => Assert.That(serverProbe.DropRequest, Is.False));
            await Pair.RunTicksSync(180);
            int generation = 0;
            int requests = 0;
            await Client.WaitAssertion(() =>
            {
                Assert.That(view.Scene, Is.Not.Null, "An open window must recover without another manual reopen.");
                Assert.That(window.IsRefreshing, Is.False);
                Assert.That(view.CaptureCamera(), Is.EqualTo(savedCamera), "Fresh geometry must not reset the camera or floor.");
                Assert.That(view.Scene!.LoadedChunks, Is.EqualTo(view.Scene.TotalChunks));
                Assert.That(view.Scene.Appearance.Any(style => (style & 0xffff) != 0), Is.True);
                generation = view.Scene.Generation;
            });
            await Server.WaitPost(() => requests = serverProbe.Requests);
            await Pair.RunTicksSync(150);
            await Server.WaitAssertion(() =>
            {
                Assert.That(serverProbe.Requests, Is.EqualTo(requests), "Successful windows must stop retrying.");
                Assert.That(_recon.BuildSnapshot(_console, _actor)!.Generation, Is.EqualTo(generation));
            });

            // Close another waiting window before its next retry. Its timer must not keep
            // requesting surveys or interfere with the following window.
            await Client.WaitPost(() => window.Close());
            await Pair.RunTicksSync(20);
            await Server.WaitPost(() =>
            {
                serverProbe.DropRequest = true;
                _ui.TryOpenUi(_console, Key, _actor);
            });
            await Pair.RunTicksSync(20);
            await Client.WaitAssertion(() =>
            {
                window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                    .OfType<CMUReconstructionWindow>().Single();
                Assert.That(window.IsRefreshing, Is.True);
                window.Close();
            });
            await Server.WaitPost(() => requests = serverProbe.Requests);
            await Pair.RunTicksSync(150);
            await Server.WaitAssertion(() =>
            {
                Assert.That(_recon.BuildSnapshot(_console, _actor), Is.Null);
                Assert.That(serverProbe.Requests, Is.EqualTo(requests));
            });
            await Server.WaitAssertion(() => Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True));
            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() =>
            {
                window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                    .OfType<CMUReconstructionWindow>().Single();
                var scene = window.FindControl<CMUReconstructionControl>("View").Scene;
                Assert.That(scene, Is.Not.Null);
                Assert.That(scene!.LoadedChunks, Is.EqualTo(scene.TotalChunks));
            });
        }
        finally
        {
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapCenterOnOpen, true));
            await Server.WaitPost(() =>
            {
                serverProbe.Target = default;
                serverProbe.DropRequest = false;
                _ui.CloseUi(_console, Key, _actor);
                Server.PlayerMan.SetAttachedEntity(session, original);
            });
            await Client.WaitPost(() =>
            {
                if (clientProbe == null) return;
                clientProbe.Target = default;
                clientProbe.DropSnapshot = false;
            });
            await Pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task MapSelectionUsesActorLocationSupportsSingleLevelPlanetsAndRemembersShipChoice()
    {
        EntityUid planet = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                planet = _maps.CreateMap(runMapInit: true);
                var grid = SEntMan.EnsureComponent<MapGridComponent>(planet);
                _maps.SetTile(planet, grid, new Vector2i(40, 50),
                    new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId));
                SEntMan.EnsureComponent<ShipFactionComponent>(_upper).Faction = "govfor";
                // Supply the same battlefield binding normally supplied by TacticalMapSystem.
#pragma warning disable RA0002
                SComp<TacticalMapComputerComponent>(_console).Map = planet;
#pragma warning restore RA0002
                _ui.TryOpenUi(_console, Key, _actor);
                Send(new CMUReconViewMessage(Vector2i.Zero) { RequestId = 10 });
                var ship = _recon.BuildSnapshot(_console, _actor)!;
                Assert.That(ship.MapChoice, Is.EqualTo(CMUReconMapChoice.Ship));
                Assert.That(ship.AboardShip && ship.HasPlanet && ship.HasShip, Is.True);
                Assert.That(ship.OperatorPosition, Is.EqualTo(new Vector2(0.5f, 1.5f)));
                Assert.That(ship.OperatorDepth, Is.Zero);

                // A fresh opening applies the persisted preference, without waiting for a survey cache.
                _ui.CloseUi(_console, Key, _actor);
                _ui.TryOpenUi(_console, Key, _actor);
                Send(new CMUReconViewMessage(Vector2i.Zero) { RequestId = 11, PreferPlanetOnShip = true });
                var remote = _recon.BuildSnapshot(_console, _actor)!;
                Assert.That(remote.MapChoice, Is.EqualTo(CMUReconMapChoice.Planet));
                Assert.That(remote.Levels, Is.EqualTo(1), "A single-level battlefield needs no Z network.");
                Assert.That(remote.Origin, Is.EqualTo(new Vector2i(32, 48)));
                Assert.That(remote.OperatorPosition, Is.Null, "A ship position must never be projected onto the planet.");
                Assert.That(remote.RequestId, Is.EqualTo(11));

                _ui.CloseUi(_console, Key, _actor);
                var transform = SEntMan.System<SharedTransformSystem>();
                transform.SetCoordinates(_actor, new EntityCoordinates(planet, new Vector2(40.5f, 50.5f)));
                transform.SetCoordinates(_console, new EntityCoordinates(planet, new Vector2(40.5f, 50.5f)));
                _ui.TryOpenUi(_console, Key, _actor);
                Send(new CMUReconViewMessage(Vector2i.Zero) { RequestId = 12 });
                var ground = _recon.BuildSnapshot(_console, _actor)!;
                Assert.That(ground.MapChoice, Is.EqualTo(CMUReconMapChoice.Planet));
                Assert.That(ground.AboardShip, Is.False);
                Assert.That(ground.HasShip, Is.True);
                Assert.That(ground.OperatorPosition, Is.EqualTo(new Vector2(40.5f, 50.5f)));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                var transform = SEntMan.System<SharedTransformSystem>();
                transform.SetCoordinates(_actor, new EntityCoordinates(_upper, new Vector2(0.5f, 1.5f)));
                transform.SetCoordinates(_console, new EntityCoordinates(_upper, new Vector2(0.5f)));
                // Clear the fixture's replicated reference before deleting its temporary planet.
#pragma warning disable RA0002
                SComp<TacticalMapComputerComponent>(_console).Map = null;
#pragma warning restore RA0002
            });
            if (planet.IsValid()) await Pair.DeleteEntityTreeLeafFirst(planet);
        }
    }

    [Test]
    public async Task OpeningCentersOnTheOperatorsFloorAndIgnoresOldMapReplies()
    {
        await Client.WaitAssertion(() =>
        {
            using var window = new CMUReconstructionWindow { CenterOnOpening = true };
            window.OpenCentered();
            window.BeginViewRequest(20);
            CMUReconSnapshotMessage Scene(int request, CMUReconMapChoice choice, Vector2? position) =>
                new(request, new Vector2i(-16, -16), -1, 2, [], [], true, 32, 32)
                {
                    RequestId = request, MapChoice = choice, HasPlanet = true, HasShip = true,
                    OperatorPosition = position, OperatorDepth = -1,
                };
            window.Receive(Scene(19, CMUReconMapChoice.Ship, Vector2.Zero));
            Assert.That(window.SurveyView.Scene, Is.Null, "A reply from an earlier selection must not replace the requested map.");
            window.Receive(Scene(20, CMUReconMapChoice.Planet, new Vector2(2.25f, 4.75f)));
            var camera = window.SurveyView.CaptureCamera();
            Assert.That(camera.Center, Is.EqualTo(new Vector2(2.25f, 4.75f)));
            Assert.That(camera.Depth, Is.EqualTo(-1));
            Assert.That(camera.Fit, Is.False);
            Assert.That(window.FindControl<Button>("CenterPlayer").Disabled, Is.False);
            Click(window.FindControl<CheckBox>("LowWalls"), Vector2.One);
            window.SurveyView.Draft.Add(new(-1, [new(1, 2)], CMUReconInk.Blue));

            window.BeginViewRequest(21);
            window.Receive(Scene(21, CMUReconMapChoice.Ship, null));
            Assert.That(window.SurveyView.CaptureCamera().Fit, Is.True);
            Assert.That(window.FindControl<Button>("CenterPlayer").Disabled, Is.True);
            Assert.That(window.SurveyView.Draft.Additions, Is.Empty);
            Click(window.FindControl<CheckBox>("LowWalls"), Vector2.One);
            window.Receive(Scene(20, CMUReconMapChoice.Planet, Vector2.Zero));
            Assert.That(window.SurveyView.Scene!.MapChoice, Is.EqualTo(CMUReconMapChoice.Ship));
            window.BeginViewRequest(22);
            window.Receive(Scene(22, CMUReconMapChoice.Planet, new Vector2(6, 7)));
            Assert.That(window.SurveyView.Draft.Additions.Single().Points[0], Is.EqualTo(new Vector2(1, 2)), "Switching maps preserves unpublished work in its own map.");
            Assert.That(window.SurveyView.CaptureCamera().LowWalls, Is.False);
            Assert.That(window.FindControl<CheckBox>("LowWalls").Pressed, Is.False, "Cutaway controls must match the restored map's camera.");
            Assert.That(window.SurveyView.CaptureCamera().Center, Is.EqualTo(new Vector2(6, 7)));

            window.CenterOnOpening = false;
            window.SurveyView.Pan(new Vector2(2, 1));
            var manual = window.SurveyView.CaptureCamera();
            window.BeginViewRequest(23);
            window.Receive(Scene(23, CMUReconMapChoice.Planet, Vector2.Zero));
            Assert.That(window.SurveyView.CaptureCamera(), Is.EqualTo(manual));
            Click(window.FindControl<Button>("CenterPlayer"), Vector2.One);
            Assert.That(window.SurveyView.CaptureCamera().Center, Is.EqualTo(Vector2.Zero));

            window.CenterOnOpening = true;
            window.RestoreCached(window.SurveyView.Scene!, window.SurveyView.CaptureCamera());
            window.BeginViewRequest(24, keepScene: true);
            var fresh = Scene(24, CMUReconMapChoice.Planet, new Vector2(5, 6));
            fresh.TotalChunks = 8;
            window.Receive(fresh);
            Assert.That(window.IsRefreshing, Is.True);
            Assert.That(window.SurveyView.CaptureCamera().Center, Is.EqualTo(new Vector2(5, 6)), "A retained map centers as soon as fresh operator metadata arrives.");
            window.SurveyView.Pan(Vector2.One);
            window.Receive(new CMUReconPatchMessage(24, [], [], true) { LoadedChunks = 8, TotalChunks = 8 });
            Assert.That(window.SurveyView.CaptureCamera().Center, Is.EqualTo(new Vector2(6, 7)), "Finishing the refresh must not undo a subsequent manual pan.");
            window.Close();
        });
    }

    [Test]
    public async Task ClassicPreferenceOpensTheOriginalInterfaceWithoutStartingASurvey()
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        try
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                Server.PlayerMan.SetAttachedEntity(session, _actor);
            });
            await Pair.RunUntilSynced();
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, true));
            await Server.WaitAssertion(() => Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True));
            await Pair.RunTicksSync(50);
            await Client.WaitAssertion(() =>
            {
                var windows = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children;
                Assert.That(windows.OfType<CMUReconstructionWindow>(), Is.Empty);
                Assert.That(windows.OfType<Content.Client._RMC14.TacticalMap.TacticalMapWindow>().Any(), Is.True);
            });
            await Server.WaitAssertion(() =>
            {
                Assert.That(_ui.IsUiOpen(_console, TacticalMapComputerUi.Key, _actor), Is.True);
                Assert.That(_ui.IsUiOpen(_console, Key, _actor), Is.False);
                Assert.That(_recon.BuildSnapshot(_console, _actor), Is.Null);
            });
        }
        finally
        {
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, false));
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, TacticalMapComputerUi.Key, _actor);
                _ui.CloseUi(_console, Key, _actor);
                Server.PlayerMan.SetAttachedEntity(session, original);
            });
            await Pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task ShipSideSelectionIsSavedByTheClientAndUsedByTheNextOpening()
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid planet = default;
        try
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                planet = _maps.CreateMap(runMapInit: true);
                var grid = SEntMan.EnsureComponent<MapGridComponent>(planet);
                _maps.SetTile(planet, grid, Vector2i.Zero, new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId));
                SEntMan.EnsureComponent<ShipFactionComponent>(_upper).Faction = "govfor";
                // Provide the battlefield binding without changing the production access contract.
#pragma warning disable RA0002
                SComp<TacticalMapComputerComponent>(_console).Map = planet;
#pragma warning restore RA0002
                Server.PlayerMan.SetAttachedEntity(session, _actor);
            });
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapPlanetOnShip, false));
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() => Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True));
            await Pair.RunTicksSync(50);
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                Assert.That(window.SurveyView.Scene!.MapChoice, Is.EqualTo(CMUReconMapChoice.Ship));
                window.OnMapSelected?.Invoke(CMUReconMapChoice.Planet);
            });
            await Pair.RunTicksSync(50);
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                Assert.That(window.SurveyView.Scene!.MapChoice, Is.EqualTo(CMUReconMapChoice.Planet));
                Assert.That(Client.ResolveDependency<IConfigurationManager>().GetCVar(CCVars.CMUTacMapPlanetOnShip), Is.True);
                window.Close();
            });
            await Pair.RunTicksSync(20);
            await Server.WaitAssertion(() => Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True));
            await Pair.RunTicksSync(50);
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                Assert.That(window.SurveyView.Scene!.MapChoice, Is.EqualTo(CMUReconMapChoice.Planet));
                Assert.That(window.SurveyView.Scene.AboardShip, Is.True);
                Assert.That(window.FindControl<Button>("CenterPlayer").Disabled, Is.True);
                window.OnMapSelected?.Invoke(CMUReconMapChoice.Ship);
            });
            await Pair.RunTicksSync(50);
            await Client.WaitAssertion(() => Assert.That(Client.ResolveDependency<IConfigurationManager>().GetCVar(CCVars.CMUTacMapPlanetOnShip), Is.False));
        }
        finally
        {
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapPlanetOnShip, false));
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                Server.PlayerMan.SetAttachedEntity(session, original);
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() =>
            {
#pragma warning disable RA0002
                SComp<TacticalMapComputerComponent>(_console).Map = null;
#pragma warning restore RA0002
            });
            if (planet.IsValid()) await Pair.DeleteEntityTreeLeafFirst(planet);
        }
    }

    [Test]
    public async Task DrawingsCrossWallsAndEmptyTerrainAndCanBeUndone()
    {
        var points = new Vector2[] { new(2.25f, 3.75f), new(6.5f, 3.75f), new(6.5f, 5.25f) };
        int generation = 0;
        int orderId = 0;
        await Server.WaitAssertion(() =>
        {
            generation = _recon.BuildSnapshot(_console, _actor)!.Generation;
            SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(_upper, new Vector2(4.5f, 3.5f)));
            _maps.SetTile(_upper, SComp<MapGridComponent>(_upper), new Vector2i(5, 3), Tile.Empty);
            Send(new CMUReconRouteMessage(generation, 0, points, CMUReconInk.Red));
            var order = _recon.BuildSnapshot(_console, _actor)!.Orders.Single();
            Assert.That(order.Waypoints, Is.EqualTo(points), "Drawing crosses both a wall and missing ground without pathfinding.");
            Assert.That(order.Ink, Is.EqualTo(CMUReconInk.Red));
            Assert.That(order.Depth, Is.Zero);
            orderId = order.Id;
        });
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            Send(new CMUReconCancelOrderMessage(generation + 1, orderId));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Has.Length.EqualTo(1));
            Send(new CMUReconCancelOrderMessage(generation, orderId));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Is.Empty);
            Send(new CMUReconRouteMessage(generation, 0, points, CMUReconInk.Green));
            Send(new CMUReconRouteMessage(generation, -1, [new(1.5f, 1.5f)], CMUReconInk.Purple));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Has.Length.EqualTo(2), "Rapid successive strokes and dots must work.");
            Send(new CMUReconClearOrdersMessage());
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Is.Empty);
        });
    }

    [Test]
    public async Task DrawingsRejectInvalidPayloadsAndStaleOrUnauthorizedViewers()
    {
        await Server.WaitAssertion(() =>
        {
            var generation = _recon.BuildSnapshot(_console, _actor)!.Generation;
            Send(new CMUReconRouteMessage(generation, -2, [new(2, 2)], CMUReconInk.Yellow));
            Send(new CMUReconRouteMessage(generation, 0, [new(float.NaN, 2)], CMUReconInk.Yellow));
            Send(new CMUReconRouteMessage(generation, 0, [new(10000, 2)], CMUReconInk.Yellow));
            Send(new CMUReconRouteMessage(generation, 0, [new(2, 2)], (CMUReconInk) 255));
            Send(new CMUReconRouteMessage(generation + 1, 0, [new(2, 2)], CMUReconInk.Yellow));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Is.Empty);
            SEntMan.System<SkillsSystem>().SetSkill(_actor, "RMCSkillLeadership", 0);
            Send(new CMUReconRouteMessage(generation, 0, [new(2, 2)], CMUReconInk.Yellow));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Is.Empty);
        });
    }
    [Test]
    public async Task CachedSceneSurvivesPartialRefreshWithoutPaletteMixing()
    {
        await Pair.RunTicksSync(40);
        CMUReconSnapshotMessage saved = null!;
        await Server.WaitPost(() => saved = _recon.BuildSnapshot(_console, _actor)!);
        await Client.WaitAssertion(() =>
        {
            using var window = new CMUReconstructionWindow();
            window.OpenCentered();
            window.Receive(saved);
            var view = window.SurveyView;
            view.Pan(new Vector2(2, 3));
            var camera = view.CaptureCamera();
            window.RestoreCached(saved, camera);
            var next = new CMUReconSnapshotMessage(saved.Generation + 1, saved.Origin, saved.MinDepth, saved.Levels,
                [], [], true, saved.Width, saved.Height) { TotalChunks = 2 };
            window.Receive(next);
            var cells = new byte[256];
            Array.Fill(cells, (byte) CMUReconMaterial.Wall);
            window.Receive(new CMUReconPatchMessage(next.Generation, [new(0, 0, 0, cells)], [], true)
                { LoadedChunks = 1, TotalChunks = 2 });
            Assert.That(view.Scene, Is.SameAs(saved), "Keep rendering the complete previous scene during refresh.");
            Assert.That(window.IsRefreshing, Is.True);
            Assert.That(window.FindControl<Button>("Clear").Disabled, Is.True);
            window.Receive(new CMUReconPatchMessage(next.Generation, [], [], true) { LoadedChunks = 2, TotalChunks = 2 });
            Assert.That(window.IsRefreshing, Is.False);
            Assert.That(view.Scene!.Generation, Is.EqualTo(next.Generation));
            Assert.That(view.Scene.Cells[0], Is.EqualTo((byte) CMUReconMaterial.Wall));
            Assert.That(view.CaptureCamera(), Is.EqualTo(camera));
            Assert.That(view.Scene.Surfaces, Is.Empty, "Old palette IDs must not leak into a different atlas.");
            window.RestoreCached(saved, camera);
            window.Receive(new CMUReconFeedbackMessage("cmu-recon-no-map"));
            Assert.That(view.Scene, Is.Null, "An unavailable map must clear the stale preview.");
            window.Close();
        });
    }

    [Test]
    public async Task FullMapIncludesDistantTilesAndTheirRealFloorAppearance()
    {
        var distant = new Vector2i(160, -90);
        EntityUid map = default;
        EntityUid console = default;
        EntityUid actor = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                // Set up a fresh map before its first survey; later tile additions are intentionally ignored.
                map = _maps.CreateMap(runMapInit: true);
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                var tile = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                _maps.SetTile(map, grid, Vector2i.Zero, tile);
                _maps.SetTile(map, grid, distant, tile);
                console = SEntMan.SpawnEntity("CMUTacticalReconstructionTableGovfor", new EntityCoordinates(map, new Vector2(0.5f)));
                SEntMan.RemoveComponent<AccessReaderComponent>(console);
                actor = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map, new Vector2(0.5f, 1.5f)));
                SEntMan.System<SkillsSystem>().SetSkill(actor, "RMCSkillLeadership", 2);
                Assert.That(_ui.TryOpenUi(console, Key, actor), Is.True);
                SEntMan.EventBus.RaiseLocalEvent(console, new CMUReconViewMessage(Vector2i.Zero) { Actor = actor, UiKey = Key });
            });
            await Pair.RunTicksSync(160);
            await Server.WaitAssertion(() =>
            {
                var snapshot = _recon.BuildSnapshot(console, actor)!;
                var local = distant - snapshot.Origin;
                Assert.That(local.X, Is.LessThan(snapshot.Width));
                Assert.That(local.Y, Is.InRange(0, snapshot.Height - 1));
                Assert.That(snapshot.Width, Is.GreaterThan(CMUReconGeometry.Size));
                var index = CMUReconGeometry.Index(local.X, local.Y, 0, snapshot.Width, snapshot.Height);
                Assert.That(snapshot.Cells[index], Is.EqualTo((byte) CMUReconMaterial.Floor));
                var style = snapshot.Surfaces.Single(s => s.Id == (snapshot.Appearance[index] & 0xffff));
                Assert.That(style.Prototype, Is.EqualTo("Plating"));
                Assert.That(style.Entity, Is.False);
                Assert.That(snapshot.LoadedChunks, Is.EqualTo(snapshot.TotalChunks));
                SEntMan.EventBus.RaiseLocalEvent(console, new CMUReconOrderMessage(snapshot.Generation, 0, distant, CMUReconOrderKind.Rally)
                    { Actor = actor, UiKey = Key });
                Assert.That(_recon.BuildSnapshot(console, actor)!.Orders.Single().Tile, Is.EqualTo(distant));
            });
        }
        finally
        {
            if (map.IsValid()) await Pair.DeleteEntityTreeLeafFirst(map);
        }
    }

    [Test]
    public async Task ClosedUiCannotSurveyAndClientWindowLoads()
    {
        CMUReconSnapshotMessage snapshot = null!;
        await Server.WaitAssertion(() =>
        {
            snapshot = _recon.BuildSnapshot(_console, _actor)!;
            _ui.CloseUi(_console, Key, _actor);
            Send(new CMUReconViewMessage(Vector2i.Zero));
            Assert.That(_recon.BuildSnapshot(_console, _actor), Is.Null);
        });
        await Client.WaitAssertion(() =>
        {
            ProtoId<ShaderPrototype> shaderId = "CMUTacticalReconstruction";
            var shader = CProtoMan.Index(shaderId).InstanceUnique();
            Assert.That(shader, Is.Not.Null);
            shader.Dispose();
            using var window = new CMUReconstructionWindow();
            window.OpenCentered();
            window.Receive(snapshot);
            var view = window.FindControl<CMUReconstructionControl>("View");
            var pencil = window.FindControl<CheckBox>("Pencil");
            Assert.That(view.Scene, Is.Not.Null);
            Assert.That(view.SelectedLevel, Is.EqualTo(1), "Select Z0 in a survey starting at Z-1.");
            Assert.That(view.DrawingEnabled, Is.False, "Dragging pans by default.");
            Assert.That(view.CaptureCamera().LowWalls, Is.True);
            Assert.That(pencil.Disabled, Is.False);
            Click(pencil, Vector2.One);
            Assert.That(view.DrawingEnabled, Is.True);
            CMUReconSendMessage sent = null;
            window.OnSend += message => sent = message;
            view.OnStroke?.Invoke([new(4.25f, 4.5f), new(5.5f, 5.75f)], -1, CMUReconInk.Green);
            Assert.That(sent, Is.Null, "Releasing the pencil must keep edits local.");
            Assert.That(view.Draft.Additions, Has.Count.EqualTo(1));
            Click(window.FindControl<Button>("Send"), Vector2.One);
            Assert.That(sent, Is.Not.Null);
            Assert.That(sent.Additions.Single().Ink, Is.EqualTo(CMUReconInk.Green));
            Assert.That(sent.Additions.Single().Depth, Is.EqualTo(-1));
            window.Receive(new CMUReconSentMessage(sent.RequestId, false, []));
            Assert.That(view.Draft.Additions, Has.Count.EqualTo(1), "Failed submissions retain the draft.");
            Click(window.FindControl<Button>("Send"), Vector2.One);
            window.Receive(new CMUReconSentMessage(sent.RequestId, true, []));
            Assert.That(view.Draft.Changed, Is.False);
            view.Pan(new Vector2(3, 2));
            var before = view.CaptureCamera();
            view.SetTopDown();
            Assert.That(view.CaptureCamera().Overhead, Is.True);
            Assert.That(view.CaptureCamera().Center, Is.EqualTo(before.Center));
            view.ResetCamera();
            Assert.That(view.CaptureCamera().Overhead, Is.False);
            Assert.That(view.CaptureCamera().Fit, Is.True);
            Assert.That(view.CaptureCamera().Center, Is.EqualTo((Vector2) snapshot.Origin + new Vector2(snapshot.Width, snapshot.Height) / 2));
            window.Receive(new CMUReconPatchMessage(snapshot.Generation, [], [], false));
            Assert.That(pencil.Disabled, Is.True);
            Assert.That(view.DrawingEnabled, Is.False, "Revoking training disables pencil input.");
            window.Close();
        });
    }

    [Test]
    public async Task AnnotationBatchValidatesAtomicallyAndPreservesWidthAndText()
    {
        var firstId = 0;
        await Server.WaitAssertion(() =>
        {
            var generation = _recon.BuildSnapshot(_console, _actor)!.Generation;
            Send(new CMUReconSendMessage(generation, 1,
                [new(0, [new(2.25f, 2.5f), new(6.75f, 6)], CMUReconInk.Blue, 8),
                 new(-1, [new(3.25f, 3.75f)], CMUReconInk.Red, 3, "Hold this entrance")], []));
            var orders = _recon.BuildSnapshot(_console, _actor)!.Orders;
            Assert.That(orders, Has.Length.EqualTo(2));
            Assert.That(orders[0].Width, Is.EqualTo(8));
            Assert.That(orders[1].Text, Is.EqualTo("Hold this entrance"));
            Assert.That(orders[1].Waypoints[0], Is.EqualTo(new Vector2(3.25f, 3.75f)));
            firstId = orders[0].Id;
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            var generation = _recon.BuildSnapshot(_console, _actor)!.Generation;
            Send(new CMUReconSendMessage(generation, 2,
                [new(0, [new(2, 2)], CMUReconInk.Blue, float.NaN)], [firstId]));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Has.Length.EqualTo(2), "Invalid additions must not apply any removals.");
            Send(new CMUReconSendMessage(generation, 3, [], [firstId]));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders.Single().Kind, Is.EqualTo(CMUReconOrderKind.Text));
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SkillsSystem>().SetSkill(_actor, "RMCSkillLeadership", 0);
            var snapshot = _recon.BuildSnapshot(_console, _actor)!;
            Send(new CMUReconSendMessage(snapshot.Generation, 4, [], [snapshot.Orders.Single().Id]));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders, Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task WaterAndFacingDoubleDoorsKeepTheirRealCategories()
    {
        await Server.WaitAssertion(() =>
        {
            SEntMan.SpawnEntity("CMFloorShallowWaterEntity", new EntityCoordinates(_upper, new Vector2(4.5f, 4.5f)));
            SEntMan.SpawnEntity("CMDoubleDoorAlmayerSolid", new EntityCoordinates(_upper, new Vector2(3.5f, 3.5f)));
            var other = SEntMan.SpawnEntity("CMDoubleDoorAlmayerSolid", new EntityCoordinates(_upper, new Vector2(3.5f, 2.5f)));
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(other, Angle.FromDegrees(180));
            Assert.That(_recon.ReadCell(_upper, new(4, 4)), Is.EqualTo((byte) CMUReconMaterial.Water));
            Assert.That(_recon.ReadCell(_upper, new(3, 3)), Is.EqualTo((byte) CMUReconMaterial.DoubleDoor));
            Assert.That(_recon.ReadCell(_upper, new(3, 2)), Is.EqualTo((byte) CMUReconMaterial.DoubleDoor));
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            var snapshot = _recon.BuildSnapshot(_console, _actor)!;
            var tile = new Vector2i(3, 2) - snapshot.Origin;
            var i = CMUReconGeometry.Index(tile.X, tile.Y, 1, snapshot.Width, snapshot.Height);
            Assert.That(snapshot.Directions[i] & 3, Is.EqualTo(2));
            Assert.That(snapshot.Surfaces.Single(s => s.Id == snapshot.Appearance[i] >> 16).Variant, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ReconstructionContactsUseNormalFactionFiltering()
    {
        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
            // Seed synthetic tracked contacts; production ownership stays with TacticalMapSystem.
#pragma warning disable RA0002
            map.GovforBlips[_actor.Id] = new TacticalMapBlip(new(2, 3), null, Color.Blue, TacticalMapBlipStatus.Alive, null, false);
            map.OpforBlips[_console.Id] = new TacticalMapBlip(new(4, 5), null, Color.Red, TacticalMapBlipStatus.Alive, null, false);
#pragma warning restore RA0002
            var computer = SComp<TacticalMapComputerComponent>(_console);
            var tactical = SEntMan.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>();
            tactical.RefreshReconstructionContacts((_console, computer));
            IReadOnlyDictionary<int, TacticalMapBlip> blips = computer.Blips;
            Assert.That(blips.ContainsKey(_actor.Id), Is.True);
            Assert.That(blips.ContainsKey(_console.Id), Is.False, "An enemy without sensor visibility must not appear on the reconstruction.");
            Assert.That(blips[_actor.Id].Indices, Is.EqualTo(new Vector2i(2, 3)));
        });
    }

    [Test]
    public async Task SparseEmptyChunkClearsOldGeometryAndDraftTextStaysLocal()
    {
        CMUReconSnapshotMessage snapshot = null;
        await Server.WaitPost(() => snapshot = _recon.BuildSnapshot(_console, _actor)!);
        await Client.WaitAssertion(() =>
        {
            Array.Fill(snapshot.Cells, (byte) CMUReconMaterial.Wall);
            Array.Fill(snapshot.Appearance, 55u);
            CMUReconSceneData.Apply(snapshot, new CMUReconPatchMessage(snapshot.Generation, [new(0, 0, 0, [], Empty: true)], [], true));
            Assert.That(snapshot.Cells[0], Is.Zero);
            Assert.That(snapshot.Appearance[0], Is.Zero);
            Assert.That(snapshot.Cells[CMUReconGeometry.Index(16, 0, 0, snapshot.Width, snapshot.Height)], Is.EqualTo((byte) CMUReconMaterial.Wall));
            using var window = new CMUReconstructionWindow();
            window.OpenCentered();
            window.Receive(snapshot);
            window.FindControl<LineEdit>("MarkerText").Text = "Medical staging";
            Click(window.FindControl<CheckBox>("PlaceText"), Vector2.One);
            Assert.That(window.SurveyView.TextEnabled, Is.True);
            window.SurveyView.OnTextPoint?.Invoke(new Vector2(2.25f, 2.75f), 0);
            Assert.That(window.SurveyView.Orders, Is.Empty);
            Assert.That(window.SurveyView.Draft.Additions.Single().Text, Is.EqualTo("Medical staging"));
            Click(window.FindControl<Button>("Undo"), Vector2.One);
            Assert.That(window.SurveyView.Draft.Changed, Is.False);
            window.Close();
        });
    }

    private static void Dispatch(Control control, string method, params object[] args) =>
        control.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, args);

    private static void Click(Control control, Vector2 at)
    {
        if (control.Size.X < 2 || control.Size.Y < 2)
        {
            control.Measure(new Vector2(120, 30));
            control.Arrange(UIBox2.FromDimensions(control.Position, new Vector2(120, 30)));
        }
        var screen = new ScreenCoordinates((Vector2) control.GlobalPixelPosition + at * control.UIScale, default);
        Dispatch(control, "KeyBindDown", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down, screen, true, at, at));
        Dispatch(control, "KeyBindUp", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up, screen, true, at, at));
    }

    [Test]
    public async Task MouseGesturesPanOrbitAndDrawAcrossStructures()
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
            view.RestoreCamera(view.CaptureCamera() with { Center = new Vector2(4.5f, 3.5f), Distance = 30, Overhead = true, Fit = false });
            var at = view.Size / 2;
            void Move(Vector2 position) => Dispatch(view, "MouseMove", new GUIMouseMoveEventArgs(Vector2.Zero,
                view, position, default, position, position * view.UIScale));
            void Press(bool down, Vector2 position) => Dispatch(view, down ? "KeyBindDown" : "KeyBindUp",
                new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, down ? BoundKeyState.Down : BoundKeyState.Up,
                    default, true, position, position * view.UIScale));

            var camera = view.CaptureCamera();
            Press(true, at);
            Move(at + new Vector2(20, 0));
            Press(false, at + new Vector2(20, 0));
            Assert.That(view.CaptureCamera().Center, Is.Not.EqualTo(camera.Center), "Left drag pans by default.");
            Assert.That(view.CaptureCamera().Yaw, Is.EqualTo(camera.Yaw));

            var input = Client.ResolveDependency<IInputManager>();
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var enabled = input.Enabled;
            try
            {
                input.Enabled = true;
                ui.SetHovered(view);
                var middle = new KeyEventArgs(Keyboard.Key.MouseMiddle, false, false, false, false, false, 0);
                input.KeyDown(middle);
                Assert.That(middle.Handled, Is.True, "Middle mouse is consumed locally without gameplay rebinding.");
                var start = ui.MousePositionScaled.Position - view.GlobalPosition;
                camera = view.CaptureCamera();
                Move(start + new Vector2(30, 10));
                Assert.That(view.CaptureCamera().Yaw, Is.Not.EqualTo(camera.Yaw));
                Assert.That(view.CaptureCamera().Center, Is.EqualTo(camera.Center));
                Assert.That(view.CaptureCamera().Overhead, Is.False);
                input.KeyUp(new KeyEventArgs(Keyboard.Key.MouseMiddle, false, false, false, false, false, 0));
                camera = view.CaptureCamera();
                Move(start + new Vector2(50, 20));
                Assert.That(view.CaptureCamera(), Is.EqualTo(camera), "Releasing middle mouse stops orbiting.");
            }
            finally
            {
                input.Enabled = enabled;
                ui.SetHovered(null);
            }

            view.SetTopDown();
            Array.Fill(snapshot.Cells, (byte) CMUReconMaterial.Wall);
            Click(window.FindControl<CheckBox>("Pencil"), Vector2.One);
            CMUReconSendMessage sent = null;
            window.OnSend += message => sent = message;
            camera = view.CaptureCamera();
            Press(true, at);
            Move(at + new Vector2(13, 7));
            Move(at + new Vector2(29, -8));
            Assert.That(sent, Is.Null, "A held pencil is a live stroke, not separate click waypoints.");
            Press(false, at + new Vector2(31, -9));
            Assert.That(sent, Is.Null, "Drawing waits for Send.");
            Click(window.FindControl<Button>("Send"), Vector2.One);
            Assert.That(sent, Is.Not.Null);
            var stroke = sent.Additions.Single();
            Assert.That(stroke.Points.Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(stroke.Points.Any(p => p.X != MathF.Floor(p.X)), Is.True);
            Assert.That(view.CaptureCamera(), Is.EqualTo(camera), "Drawing must not pan the map.");
            var ink = new CMUReconOrder(77, new(4, 3), 0, CMUReconOrderKind.Route, stroke.Points, stroke.Ink);
            window.Receive(new CMUReconPatchMessage(snapshot.Generation, [], [ink], true));
            window.Receive(new CMUReconPatchMessage(snapshot.Generation, [], [], true) { OrdersChanged = false });
            Assert.That(view.Orders, Has.Length.EqualTo(1), "Terrain-only patches must preserve shared pencil strokes.");
            window.Close();
        });
    }
}

/// <summary>Deterministically drops an opening handshake without replacing the real BUI transport.</summary>
public sealed class CMUReconHandshakeTestSystem : EntitySystem
{
    public void RequestPreload() => RaiseNetworkEvent(new CMUReconPreloadRequest(12345, false));
    public EntityUid Target;
    public bool DropRequest;
    public bool DropSnapshot;
    public int Requests;
    public int Chunks;
    public int ChunkBytes;
    public CMUReconSnapshotMessage Snapshot;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BoundUserInterfaceMessageAttempt>(OnMessage);
    }

    private void OnMessage(BoundUserInterfaceMessageAttempt args)
    {
        if (args.Target != Target)
            return;
        if (args.Message is CMUReconPatchMessage patch)
        {
            Chunks += patch.Chunks.Length;
            ChunkBytes += patch.Chunks.Sum(CMUReconChunkEncoding.EstimatedBytes);
        }
        if (args.Message is CMUReconSnapshotMessage snapshot) Snapshot = snapshot;
        if (args.Message is CMUReconViewMessage)
        {
            Requests++;
            if (DropRequest)
            {
                DropRequest = false;
                args.Cancel();
            }
        }
        else if (args.Message is CMUReconSnapshotMessage && DropSnapshot)
        {
            DropSnapshot = false;
            args.Cancel();
        }
    }
}
