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
}
