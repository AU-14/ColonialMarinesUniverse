using System.Linq;
using System.Numerics;
using Content.Client.Actions;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Xenomorphs.Swarm;
using Content.Server.NPC.Components;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.Actions;
using Content.Shared.CMU14.Xenomorphs.Swarm;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUSwarmOrdersTest : GameTest
{
    private static readonly ProtoId<NpcFactionPrototype> EnemyFaction = "GOVFOR";

    private EntityUid _queen;
    private EntityUid _hive;
    private CMUSwarmControllerComponent _swarm = default!;

    private void Setup(EntityCoordinates coordinates, int count = 3)
    {
        var hives = SEntMan.System<SharedXenoHiveSystem>();
        _hive = SEntMan.SpawnEntity("CMXenoHive", coordinates);
        _queen = SEntMan.SpawnEntity("CMXenoQueen", coordinates.Offset(new Vector2(-3, 0)));
        SEntMan.GetComponent<CMUQueenSwarmComponent>(_queen).Enabled = true;
        hives.SetHive(_queen, _hive);
        hives.SetHiveQueen(_queen, (_hive, SEntMan.GetComponent<HiveComponent>(_hive)));
        SEntMan.EnsureComponent<XenoAttachedOvipositorComponent>(_queen);
        var attached = new XenoOvipositorChangedEvent(true, _queen, _hive);
        SEntMan.EventBus.RaiseLocalEvent(_queen, ref attached);
        _swarm = SEntMan.EnsureComponent<CMUSwarmControllerComponent>(_hive);
        for (var i = 0; i < count; i++)
        {
            var lesser = SEntMan.SpawnEntity("CMUSwarmLesser", coordinates);
            hives.SetHive(lesser, _hive);
            _swarm.Members.Add(lesser);
            var agent = SEntMan.EnsureComponent<CMUSwarmAgentComponent>(lesser);
            agent.Home = coordinates;
            agent.Order = new CMUSwarmOrder { Kind = CMUSwarmOrderKind.Idle };
        }
    }

    private void Send(CMUSwarmCommand command, IEnumerable<EntityUid> members,
        CMUSwarmOrderKind kind = CMUSwarmOrderKind.Guard, int squad = 0,
        CMUSwarmAggression aggression = CMUSwarmAggression.Defensive, bool append = false, EntityUid? actor = null)
    {
        SEntMan.GetComponent<CMUSwarmCommanderComponent>(_queen).NextCommand = TimeSpan.Zero;
        var message = new CMUSwarmCommandMessage(command, members.Select(member => SEntMan.GetNetEntity(member)).ToList(),
            squad, kind, aggression, append: append) { Actor = actor ?? _queen, UiKey = CMUSwarmUiKey.Key };
        SEntMan.EventBus.RaiseLocalEvent(_queen, (object) message);
    }

    private void Place(IEnumerable<EntityUid> members, CMUSwarmOrderKind kind, EntityCoordinates coordinates, bool append = false)
    {
        Send(CMUSwarmCommand.Prepare, members, kind, append: append);
        var action = new CMUPlaceSwarmOrderActionEvent { Performer = _queen, Target = coordinates };
        SEntMan.EventBus.RaiseLocalEvent(_queen, action);
        Assert.That(action.Handled, Is.True);
    }

    private void Think()
    {
        _swarm.NextThink = TimeSpan.Zero;
        SEntMan.System<CMUQueenSwarmSystem>().Update(0);
    }

    [Test]
    public async Task OviPanelReplicatesSelectionCommandsAndClosesOnDetach()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var previous = session.AttachedEntity;
        NetEntity queenNet = default;
        NetEntity memberNet = default;
        await Server.WaitAssertion(() =>
        {
            Setup(map.GridCoords, 2);
            _swarm.Passive = true;
            queenNet = SEntMan.GetNetEntity(_queen);
            memberNet = SEntMan.GetNetEntity(_swarm.Members[0]);
            Server.PlayerMan.SetAttachedEntity(session, _queen);
        });
        try
        {
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var actions = CEntMan.System<ActionsSystem>();
                var menu = actions.GetActions(CEntMan.GetEntity(queenNet)).Single(a => actions.GetEvent(a) is CMUSwarmMenuActionEvent);
                Assert.That(menu.Comp.Enabled, Is.True);
                actions.TriggerAction(menu);
            });
            // RunUntilSynced only waits for the server tick at entry, not a complete action/reply round trip.
            await Pair.ReallyBeIdle();
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                var ui = SEntMan.System<SharedUserInterfaceSystem>();
                Assert.That(ui.IsUiOpen(_queen, CMUSwarmUiKey.Key), Is.True,
                    "The server must accept the attached Queen's menu action.");
            });
            await Client.WaitAssertion(() =>
            {
                var ui = CEntMan.System<SharedUserInterfaceSystem>();
                var queen = CEntMan.GetEntity(queenNet);
                Assert.That(ui.IsUiOpen(queen, CMUSwarmUiKey.Key), Is.True);
                Assert.That(ui.TryGetUiState<CMUSwarmUiState>(queen, CMUSwarmUiKey.Key, out var state), Is.True);
                Assert.That(state!.Members.Count, Is.EqualTo(2));
                ui.ClientSendUiMessage(queen, CMUSwarmUiKey.Key,
                    new CMUSwarmCommandMessage(CMUSwarmCommand.AssignSquad, [memberNet], squad: 3));
            });
            await Pair.ReallyBeIdle();
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<CMUSwarmAgentComponent>(_swarm.Members[0]).Squad, Is.EqualTo(3));
                Assert.That(SEntMan.GetComponent<CMUSwarmAgentComponent>(_swarm.Members[1]).Squad, Is.Zero);
                SEntMan.EventBus.RaiseLocalEvent(_queen, new XenoGrowOvipositorActionEvent
                {
                    Performer = _queen, DetachDoAfter = TimeSpan.Zero,
                });
            });
            await Pair.RunTicksSync(5);
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => Assert.That(CEntMan.System<SharedUserInterfaceSystem>()
                .IsUiOpen(CEntMan.GetEntity(queenNet), CMUSwarmUiKey.Key), Is.False));
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, previous));
            await Pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task SquadSelectionAndPatrolEditsLeaveOtherUnitsAloneAndSurviveOviExit()
    {
        var map = await Pair.CreateTestMap();
        CMUSwarmOrder? original = null;
        await Server.WaitAssertion(() =>
        {
            Setup(map.GridCoords);
            var selected = _swarm.Members.Take(2).ToArray();
            Send(CMUSwarmCommand.AssignSquad, selected, squad: 2);
            Place(selected, CMUSwarmOrderKind.Patrol, map.GridCoords.Offset(new Vector2(3, 0)));
            original = SEntMan.GetComponent<CMUSwarmAgentComponent>(selected[0]).Order;
            Place([selected[0]], CMUSwarmOrderKind.Patrol, map.GridCoords.Offset(new Vector2(5, 0)), append: true);
            var first = SEntMan.GetComponent<CMUSwarmAgentComponent>(selected[0]);
            var second = SEntMan.GetComponent<CMUSwarmAgentComponent>(selected[1]);
            var third = SEntMan.GetComponent<CMUSwarmAgentComponent>(_swarm.Members[2]);
            Assert.Multiple(() =>
            {
                Assert.That(first.Squad, Is.EqualTo(2));
                Assert.That(first.Order!.Flags.Count, Is.EqualTo(2));
                Assert.That(second.Order, Is.SameAs(original));
                Assert.That(second.Order!.Flags.Count, Is.EqualTo(1), "Appending a subset's route must not mutate the squad's shared order.");
                Assert.That(third.Order!.Kind, Is.EqualTo(CMUSwarmOrderKind.Idle));
            });
            SEntMan.EventBus.RaiseLocalEvent(_queen, new XenoGrowOvipositorActionEvent
            {
                Performer = _queen, DetachDoAfter = TimeSpan.Zero,
            });
        });
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<XenoAttachedOvipositorComponent>(_queen), Is.False);
            Send(CMUSwarmCommand.Release, _swarm.Members);
            Assert.That(SEntMan.GetComponent<CMUSwarmAgentComponent>(_swarm.Members[1]).Order, Is.SameAs(original),
                "A stale UI command after detaching must be rejected without cancelling the active route.");
            Assert.That(SEntMan.GetComponent<CMUSwarmCommanderComponent>(_queen).Selection, Is.Empty);
            var commands = SEntMan.System<SharedActionsSystem>().GetActions(_queen)
                .Where(a => SEntMan.System<SharedActionsSystem>().GetEvent(a) is CMUSwarmMenuActionEvent or CMUPlaceSwarmOrderActionEvent);
            Assert.That(commands.All(a => !a.Comp.Enabled), Is.True, "Ovi removal must revoke the action controls too.");
            Think();
            Assert.That(SEntMan.GetComponent<NPCSteeringComponent>(_swarm.Members[1]).Coordinates.EntityId,
                Is.EqualTo(original!.Flags[0]), "The persistent route must continue to drive movement.");
        });
    }

    [Test]
    public async Task ForeignActorsAndForeignLessersCannotReceiveOrders()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Setup(map.GridCoords, 1);
            var foreign = SEntMan.SpawnEntity("CMUSwarmLesser", map.GridCoords);
            var member = _swarm.Members[0];
            var before = SEntMan.GetComponent<CMUSwarmAgentComponent>(member).Order;
            Send(CMUSwarmCommand.AssignSquad, [member], squad: 3, actor: foreign);
            Assert.That(SEntMan.GetComponent<CMUSwarmAgentComponent>(member).Squad, Is.Zero);
            Send(CMUSwarmCommand.Prepare, [foreign]);
            var invalid = new CMUPlaceSwarmOrderActionEvent { Performer = _queen, Target = map.GridCoords };
            SEntMan.EventBus.RaiseLocalEvent(_queen, invalid);
            Assert.That(invalid.Handled, Is.False);
            Assert.That(SEntMan.GetComponent<CMUSwarmAgentComponent>(member).Order, Is.SameAs(before));
        });
    }

    [Test]
    public async Task IdleLesserPausesRestsAndResumesInsideItsRoamingArea()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Setup(map.GridCoords, 1);
            _swarm.Passive = true;
            for (var x = -11; x <= 11; x++)
            for (var y = -8; y <= 8; y++)
                SEntMan.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
            var member = _swarm.Members[0];
            var agent = SEntMan.GetComponent<CMUSwarmAgentComponent>(member);
            Think();
            Assert.That(agent.Destination, Is.Not.Null, "An unassigned lesser must choose an area destination.");
            var destination = agent.Destination!.Value;
            Assert.That(destination.Position.X - map.GridCoords.X, Is.InRange(-10f, 10f));
            Assert.That(destination.Position.Y - map.GridCoords.Y, Is.InRange(-7f, 7f));
            SEntMan.System<SharedTransformSystem>().SetCoordinates(member, destination);
            Think();
            Assert.That(agent.PauseUntil, Is.GreaterThan(SGameTiming.CurTime));
            Assert.That(SEntMan.HasComponent<NPCSteeringComponent>(member), Is.False, "Arrival must stop movement rather than choose another target immediately.");
            Think();
            Assert.That(agent.Destination, Is.Null);
            agent.RestAt = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
            Think();
            Assert.That(SEntMan.HasComponent<XenoRestingComponent>(member), Is.True, "Idle rest must use the real xeno resting state.");
            agent.WakeAt = SGameTiming.CurTime;
            agent.PauseUntil = TimeSpan.Zero;
            Think();
            Assert.That(SEntMan.HasComponent<XenoRestingComponent>(member), Is.False);
            Assert.That(agent.Destination, Is.Not.Null, "A rested lesser should resume roaming.");
        });
    }

    [Test]
    public async Task PerUnitAggressionRestrictsSharedSightings()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Setup(map.GridCoords);
            Place(_swarm.Members, CMUSwarmOrderKind.Guard, map.GridCoords);
            Send(CMUSwarmCommand.Aggression, [_swarm.Members[0]], aggression: CMUSwarmAggression.Passive);
            Send(CMUSwarmCommand.Aggression, [_swarm.Members[1]], aggression: CMUSwarmAggression.Defensive);
            var enemy = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(5, 0)));
            SEntMan.AddComponent<MobStateComponent>(enemy);
            SEntMan.System<NpcFactionSystem>().AddFaction(enemy, EnemyFaction);
            Think();
            Assert.That(SEntMan.HasComponent<NPCMeleeCombatComponent>(_swarm.Members[0]), Is.False);
            Assert.That(SEntMan.HasComponent<NPCMeleeCombatComponent>(_swarm.Members[1]), Is.False);
            Assert.That(SEntMan.GetComponent<NPCMeleeCombatComponent>(_swarm.Members[2]).Target, Is.EqualTo(enemy));
            SEntMan.System<SharedTransformSystem>().SetCoordinates(enemy, map.GridCoords.Offset(new Vector2(2, 0)));
            Think();
            Assert.That(SEntMan.GetComponent<NPCMeleeCombatComponent>(_swarm.Members[1]).Target, Is.EqualTo(enemy));
            Assert.That(SEntMan.HasComponent<NPCMeleeCombatComponent>(_swarm.Members[0]), Is.False);
        });
    }

    [Test]
    public async Task LargeSwarmDecisionsAreSpreadAcrossUpdates()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Setup(map.GridCoords, 96);
            _swarm.Passive = true;
            var system = SEntMan.System<CMUQueenSwarmSystem>();
            system.Update(0);
            Assert.That(_swarm.Members.Count(m => SEntMan.GetComponent<CMUSwarmAgentComponent>(m).RestAt > TimeSpan.Zero), Is.EqualTo(32));
            system.Update(0);
            system.Update(0);
            Assert.That(_swarm.Members.All(m => SEntMan.GetComponent<CMUSwarmAgentComponent>(m).RestAt > TimeSpan.Zero), Is.True,
                "Deferred members must get their turn, not starve behind the first batch.");
        });
    }

    [TestCase(CMUSwarmOrderKind.Weed)]
    [TestCase(CMUSwarmOrderKind.Build)]
    public async Task WorkOrdersUseNativePlacementAndDoNotDuplicateCompletedSites(CMUSwarmOrderKind kind)
    {
        var map = await Pair.CreateTestMap();
        var site = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
        EntityUid worker = default;
        await Server.WaitAssertion(() =>
        {
            Setup(site, 2);
            _swarm.Passive = true;
            // Disable the early-round hive instant-build allowance so the real construction do-after runs.
            while (SEntMan.System<SharedXenoConstructionSystem>().TryUseHiveInstantBuild(_hive))
            {
            }
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            for (var y = -2; y <= 2; y++)
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
            worker = _swarm.Members[0];
            var initialPlasma = SEntMan.GetComponent<XenoPlasmaComponent>(worker).Plasma;
            if (kind == CMUSwarmOrderKind.Build)
            {
                SEntMan.SpawnEntity("XenoWeeds", site);
                SEntMan.System<SharedTransformSystem>().SetCoordinates(worker, site.Offset(new Vector2(1, 0)));
            }
            Place(_swarm.Members, kind, site);
            Think();
            if (kind == CMUSwarmOrderKind.Build)
                Assert.That(SEntMan.GetComponent<DoAfterComponent>(worker).DoAfters.Values.Any(d => !d.Cancelled && !d.Completed), Is.True,
                    "Construction must wait for the normal interruptible do-after.");
            else
            {
                Assert.That(SEntMan.GetComponent<XenoPlasmaComponent>(worker).Plasma,
                    Is.EqualTo(initialPlasma),
                    "The lesser action must refuse isolated weed planting without charging plasma.");
                var nearby = SEntMan.SpawnEntity("XenoWeedsSource", site.Offset(new Vector2(3, 0)));
                SEntMan.System<SharedXenoHiveSystem>().SetHive(nearby, _hive);
                foreach (var member in _swarm.Members)
                    SEntMan.GetComponent<CMUSwarmAgentComponent>(member).NextWork = TimeSpan.Zero;
                Think();
                var plasma = SEntMan.GetComponent<XenoPlasmaComponent>(worker).Plasma;
                Assert.That(plasma.Float(), Is.LessThan(initialPlasma.Float()), "Planting must pay native plasma.");
            }
        });
        await Pair.RunTicksSync(420);
        await Server.WaitAssertion(() =>
        {
            var expected = kind == CMUSwarmOrderKind.Weed ? "XenoWeedsSource" : "WallXenoResin";
            var count = 0;
            using var entities = SEntMan.System<RMCMapSystem>().GetAnchoredEntitiesEnumerator<MetaDataComponent>(site);
            while (entities.MoveNext(out var entity))
            {
                if (SEntMan.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID == expected)
                    count++;
            }
            Assert.That(count, Is.EqualTo(1), "A squad must create only one structure at its shared work flag.");
            Assert.That(SEntMan.GetComponent<CMUSwarmAgentComponent>(worker).Order!.Complete, Is.True);
        });
    }
}
