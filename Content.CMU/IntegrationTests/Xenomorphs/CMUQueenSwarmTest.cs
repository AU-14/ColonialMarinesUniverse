using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Xenomorphs.Swarm;
using Content.Server.NPC.Components;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared.CMU14.Xenomorphs.Swarm;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUQueenSwarmTest : GameTest
{
    private static readonly ProtoId<NpcFactionPrototype> EnemyFaction = "GOVFOR";

    [Test]
    public async Task LesserNavigatesAroundWallToGuardMarker()
    {
        var map = await Pair.CreateTestMap();
        EntityUid lesser = default;
        var destination = map.GridCoords.Offset(new Vector2(4.5f, 0.5f));
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -2; x <= 7; x++)
            for (var y = -3; y <= 3; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var swarm = SEntMan.AddComponent<CMUSwarmControllerComponent>(hive);
            swarm.FormationRadius = 0;
            swarm.Anchor = SEntMan.SpawnEntity("CMUSwarmGuardMarker", destination);
            swarm.Marker = swarm.Anchor;
            lesser = SEntMan.SpawnEntity("CMUSwarmLesser", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            SEntMan.System<SharedXenoHiveSystem>().SetHive(lesser, hive);
            swarm.Members.Add(lesser);
            SEntMan.SpawnEntity("WallSolid", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
        });

        await Pair.RunTicksSync(600);
        await Server.WaitAssertion(() =>
        {
            var position = SEntMan.GetComponent<TransformComponent>(lesser).Coordinates;
            Assert.That(position.TryDistance(SEntMan, destination, out var distance), Is.True);
            Assert.That(distance, Is.LessThan(1), "The lesser must move using a route around the wall, without a player attached.");
        });
    }

    [Test]
    public async Task EyeModeCanCommandAcrossWallsAndBodyModeCannot()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hiveSystem = SEntMan.System<SharedXenoHiveSystem>();
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            hiveSystem.SetHive(queen, hive);
            hiveSystem.SetHiveQueen(queen, (hive, SEntMan.GetComponent<HiveComponent>(hive)));
            var core = SEntMan.SpawnEntity("HiveCoreXeno", map.GridCoords);
            hiveSystem.SetHive(core, hive);
            SEntMan.SpawnEntity("WallSolid", map.GridCoords.Offset(new Vector2(1, 0)));
            var target = map.GridCoords.Offset(new Vector2(2, 0));
            var bodyOrder = new CMUGuardSwarmActionEvent { Performer = queen, Target = target };
            SEntMan.EventBus.RaiseLocalEvent(queen, bodyOrder);
            Assert.That(bodyOrder.Handled, Is.False, "Body commands must not see through walls.");

            SEntMan.EventBus.RaiseLocalEvent(queen, new QueenEyeActionEvent { Performer = queen });
            var eyeOrder = new CMUGuardSwarmActionEvent { Performer = queen, Target = target };
            SEntMan.EventBus.RaiseLocalEvent(queen, eyeOrder);
            Assert.That(eyeOrder.Handled, Is.True, "Eye commands must use hive vision rather than body line of sight.");

            SEntMan.EventBus.RaiseLocalEvent(queen, new QueenEyeActionEvent { Performer = queen });
            var visibleOrder = new CMUGuardSwarmActionEvent { Performer = queen, Target = map.GridCoords };
            SEntMan.EventBus.RaiseLocalEvent(queen, visibleOrder);
            Assert.That(visibleOrder.Handled, Is.True, "The same actions remain usable after leaving eye mode.");
        });
    }
    [Test]
    public async Task SummonsAccumulateOnlyAfterHiveCooldown()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hiveSystem = SEntMan.System<SharedXenoHiveSystem>();
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            hiveSystem.SetHive(queen, hive);
            hiveSystem.SetHiveQueen(queen, (hive, SEntMan.GetComponent<HiveComponent>(hive)));
            var missingCore = new CMUSummonSwarmActionEvent { Performer = queen };
            SEntMan.EventBus.RaiseLocalEvent(queen, missingCore);
            Assert.That(missingCore.Handled, Is.False);

            var core = SEntMan.SpawnEntity("HiveCoreXeno", map.GridCoords);
            hiveSystem.SetHive(core, hive);
            var first = new CMUSummonSwarmActionEvent { Performer = queen };
            SEntMan.EventBus.RaiseLocalEvent(queen, first);
            Assert.That(first.Handled, Is.True);
            var swarm = SEntMan.GetComponent<CMUSwarmControllerComponent>(hive);
            var survivors = swarm.Members.ToArray();
            Assert.That(survivors, Has.Length.EqualTo(swarm.SpawnCount));

            var blocked = new CMUSummonSwarmActionEvent { Performer = queen };
            SEntMan.EventBus.RaiseLocalEvent(queen, blocked);
            Assert.That(blocked.Handled, Is.False);
            Assert.That(swarm.Members, Is.EquivalentTo(survivors));

            // Simulate expiry without waiting through the summon cooldown in the integration harness.
            swarm.NextSummon = SGameTiming.CurTime;
            var second = new CMUSummonSwarmActionEvent { Performer = queen };
            SEntMan.EventBus.RaiseLocalEvent(queen, second);
            Assert.That(second.Handled, Is.True);
            Assert.That(swarm.Members, Has.Count.EqualTo(survivors.Length * 2));
            Assert.That(swarm.Members, Is.SupersetOf(survivors));
        });
    }

    [Test]
    public async Task OneSightedEnemyRecruitsSwarmThenTheyReturnToGuard()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hiveSystem = SEntMan.System<SharedXenoHiveSystem>();
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            hiveSystem.SetHive(queen, hive);
            hiveSystem.SetHiveQueen(queen, (hive, SEntMan.GetComponent<HiveComponent>(hive)));
            var guard = new CMUGuardSwarmActionEvent { Performer = queen, Target = map.GridCoords };
            SEntMan.EventBus.RaiseLocalEvent(queen, guard);
            Assert.That(guard.Handled, Is.True);
            var swarm = SEntMan.GetComponent<CMUSwarmControllerComponent>(hive);
            var scout = SEntMan.SpawnEntity("CMUSwarmLesser", map.GridCoords.Offset(new Vector2(3, 0)));
            var follower = SEntMan.SpawnEntity("CMUSwarmLesser", map.GridCoords.Offset(new Vector2(-3, 0)));
            hiveSystem.SetHive(scout, hive);
            hiveSystem.SetHive(follower, hive);
            swarm.Members.AddRange([scout, follower]);
            swarm.SightRange = 3;
            var hostile = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(5, 0)));
            SEntMan.AddComponent<MobStateComponent>(hostile);
            SEntMan.System<NpcFactionSystem>().AddFaction(hostile, EnemyFaction);

            var system = SEntMan.System<CMUQueenSwarmSystem>();
            system.Update(0);
            Assert.That(SEntMan.GetComponent<NPCMeleeCombatComponent>(scout).Target, Is.EqualTo(hostile));
            Assert.That(SEntMan.GetComponent<NPCMeleeCombatComponent>(follower).Target, Is.EqualTo(hostile),
                "The follower is outside its own sight range and must use the scout's sighting.");

            SEntMan.DeleteEntity(hostile);
            swarm.NextThink = TimeSpan.Zero;
            system.Update(0);
            Assert.That(SEntMan.HasComponent<NPCMeleeCombatComponent>(follower), Is.False);
            Assert.That(SEntMan.GetComponent<NPCSteeringComponent>(follower).Coordinates.EntityId, Is.EqualTo(swarm.Marker));
        });
    }
}
