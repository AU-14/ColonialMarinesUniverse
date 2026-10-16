using System.Numerics;
using Content.Client.Actions;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking.Presets;
using Content.Server.CMU14.Xenomorphs.Swarm;
using Content.Server.NPC.Components;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.CMU14.Xenomorphs.Swarm;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
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

    [TestCase("DistressSignal", true)]
    [TestCase("ColonyFall", false)]
    public async Task RoundModeRestrictsSummoningUnlessQueenOverridesIt(string preset, bool allowed)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var round = SEntMan.System<AuRoundSystem>();
            var previous = round.SelectedPreset;
            try
            {
                round.SetPreset(SProtoMan.Index<GamePresetPrototype>(preset));
                var hives = SEntMan.System<SharedXenoHiveSystem>();
                var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
                var queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
                hives.SetHive(queen, hive);
                hives.SetHiveQueen(queen, (hive, SEntMan.GetComponent<HiveComponent>(hive)));
                hives.SetHive(SEntMan.SpawnEntity("HiveCoreXeno", map.GridCoords), hive);
                var settings = SEntMan.GetComponent<CMUQueenSwarmComponent>(queen);
                var swarm = SEntMan.EnsureComponent<CMUSwarmControllerComponent>(hive);

                void Summon(bool expected)
                {
                    swarm.NextSummon = SGameTiming.CurTime;
                    var count = swarm.Members.Count;
                    var action = new CMUSummonSwarmActionEvent { Performer = queen };
                    SEntMan.EventBus.RaiseLocalEvent(queen, action);
                    Assert.That(action.Handled, Is.EqualTo(expected));
                    Assert.That(swarm.Members.Count, Is.EqualTo(count + (expected ? swarm.SpawnCount : 0)));
                    if (!expected)
                        Assert.That(swarm.NextSummon, Is.EqualTo(SGameTiming.CurTime), "Disabled summons must not consume cooldown.");
                }

                Summon(allowed);
                settings.Enabled = !allowed;
                Summon(!allowed);
                settings.Enabled = null;
                Summon(allowed);
            }
            finally
            {
                round.SetPreset(previous);
            }
        });
    }

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: CMBarricadeMetal
          id: CMUTestSwarmBarbedBarricade
          components:
          - type: Barbed
            isBarbed: true
        """;

    [Test]
    public async Task AttachedQueenCanSummonFromRemoteEyeThroughClientAction()
    {
        var map = await Pair.CreateTestMap();
        EntityUid queen = default;
        EntityUid hive = default;
        NetEntity netQueen = default;
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        await Server.WaitAssertion(() =>
        {
            var hives = SEntMan.System<SharedXenoHiveSystem>();
            hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            SEntMan.GetComponent<CMUQueenSwarmComponent>(queen).Enabled = true;
            netQueen = SEntMan.GetNetEntity(queen);
            hives.SetHive(queen, hive);
            hives.SetHiveQueen(queen, (hive, SEntMan.GetComponent<HiveComponent>(hive)));
            var core = SEntMan.SpawnEntity("HiveCoreXeno", map.GridCoords);
            hives.SetHive(core, hive);
            SEntMan.EventBus.RaiseLocalEvent(queen, new XenoGrowOvipositorActionEvent
            {
                Performer = queen,
                AttachDoAfter = TimeSpan.Zero,
                AttachPlasmaCost = 0,
            });
        });
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<XenoAttachedOvipositorComponent>(queen), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(queen, new QueenEyeActionEvent { Performer = queen });
            var remote = map.GridCoords.Offset(new Vector2(30, 0));
            SEntMan.System<SharedMapSystem>().SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(30, 0), map.Tile.Tile);
            SEntMan.SpawnEntity("XenoHiveWeedsCore", remote);
            var eye = SEntMan.GetComponent<QueenEyeActionComponent>(queen).Eye!.Value;
            SEntMan.System<SharedTransformSystem>().SetCoordinates(eye, remote);
        });
        try
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, queen));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var actions = CEntMan.System<ActionsSystem>();
                var summon = actions.GetActions(CEntMan.GetEntity(netQueen)).Single(a => actions.GetEvent(a) is CMUSummonSwarmActionEvent);
                actions.TriggerAction(summon);
            });
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.System<QueenEyeSystem>().IsInQueenEye(queen), Is.True);
                var swarm = SEntMan.GetComponent<CMUSwarmControllerComponent>(hive);
                Assert.That(swarm.Members.Count, Is.EqualTo(swarm.SpawnCount));
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
            await Pair.RunUntilSynced();
        }
    }

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

    [TestCase(false)]
    [TestCase(true)]
    public async Task CoincidentLessersSpreadOutInsteadOfStacking(bool attacking)
    {
        var map = await Pair.CreateTestMap();
        var lessers = new List<EntityUid>();
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -6; x <= 6; x++)
            for (var y = -6; y <= 6; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var swarm = SEntMan.AddComponent<CMUSwarmControllerComponent>(hive);
            swarm.Passive = !attacking;
            swarm.Anchor = SEntMan.SpawnEntity("CMUSwarmGuardMarker", map.GridCoords);
            if (attacking)
            {
                var enemy = SEntMan.SpawnEntity(null, map.GridCoords);
                SEntMan.AddComponent<MobStateComponent>(enemy);
                SEntMan.System<NpcFactionSystem>().AddFaction(enemy, EnemyFaction);
            }
            for (var i = 0; i < 30; i++)
            {
                var lesser = SEntMan.SpawnEntity("CMUSwarmLesser", map.GridCoords);
                SEntMan.System<SharedXenoHiveSystem>().SetHive(lesser, hive);
                swarm.Members.Add(lesser);
                lessers.Add(lesser);
            }
        });
        await Pair.RunTicksSync(600);
        await Server.WaitAssertion(() =>
        {
            var positions = lessers.Select(uid => SEntMan.GetComponent<TransformComponent>(uid).Coordinates.Position).ToArray();
            var visibleMembers = positions.Count(pos => positions.Count(other => Vector2.DistanceSquared(pos, other) < 0.09f) == 1);
            Assert.That(visibleMembers, Is.GreaterThanOrEqualTo(24), "Accumulated summons must spread into individually visible positions.");
        });
    }

    [TestCase("DoorXenoResin")]
    [TestCase("CMAirlock")]
    public async Task SwarmNavigatesThroughClosedDoor(string prototype)
    {
        var map = await Pair.CreateTestMap();
        var lessers = new List<EntityUid>();
        EntityUid door = default;
        var destination = map.GridCoords.Offset(new Vector2(7.5f, 0.5f));
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -1; x <= 10; x++)
            for (var y = -3; y <= 3; y++)
            {
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                if (x <= 4 && y != 0 || x == -1 || x == 10 || y == -3 || y == 3)
                    SEntMan.SpawnEntity("WallSolid", map.GridCoords.Offset(new Vector2(x + 0.5f, y + 0.5f)));
            }
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var swarm = SEntMan.AddComponent<CMUSwarmControllerComponent>(hive);
            swarm.Anchor = SEntMan.SpawnEntity("CMUSwarmGuardMarker", destination);
            swarm.Marker = swarm.Anchor;
            for (var i = 0; i < swarm.SpawnCount; i++)
            {
                var lesser = SEntMan.SpawnEntity("CMUSwarmLesser", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
                SEntMan.System<SharedXenoHiveSystem>().SetHive(lesser, hive);
                swarm.Members.Add(lesser);
                lessers.Add(lesser);
            }
            door = SEntMan.SpawnEntity(prototype, map.GridCoords.Offset(new Vector2(3.5f, 0.5f)));
            SEntMan.System<SharedXenoHiveSystem>().SetHive(door, hive);
        });

        await Pair.RunTicksSync(900);
        await Server.WaitAssertion(() =>
        {
            foreach (var lesser in lessers)
            {
                var position = SEntMan.GetComponent<TransformComponent>(lesser).Coordinates;
                Assert.That(position.TryDistance(SEntMan, destination, out var distance), Is.True);
                Assert.That(position.X, Is.GreaterThan(4.5f),
                    $"Every lesser must cross the closed {prototype}; position: {position}, door: {SEntMan.GetComponent<DoorComponent>(door).State}.");
                Assert.That(distance, Is.LessThan(3), "The swarm must reach its formation in the room beyond the corridor.");
            }
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
            SEntMan.GetComponent<CMUQueenSwarmComponent>(queen).Enabled = true;
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
            SEntMan.GetComponent<CMUQueenSwarmComponent>(queen).Enabled = true;
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

    [TestCase(true)]
    [TestCase(false)]
    public async Task PassiveAndAlliancesStopCombatAndApplyToNewMembers(bool passive)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hives = SEntMan.System<SharedXenoHiveSystem>();
            var hive = SEntMan.SpawnEntity("CMUCorruptedHive", map.GridCoords);
            var queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            SEntMan.GetComponent<CMUQueenSwarmComponent>(queen).Enabled = true;
            hives.SetHive(queen, hive);
            hives.SetHiveQueen(queen, (hive, SEntMan.GetComponent<HiveComponent>(hive)));
            SEntMan.EventBus.RaiseLocalEvent(queen, new CMUGuardSwarmActionEvent { Performer = queen, Target = map.GridCoords });
            var swarm = SEntMan.GetComponent<CMUSwarmControllerComponent>(hive);
            var lesser = SEntMan.SpawnEntity("CMUSwarmLesser", map.GridCoords);
            hives.SetHive(lesser, hive);
            swarm.Members.Add(lesser);
            var hostile = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(2, 0)));
            SEntMan.AddComponent<MobStateComponent>(hostile);
            SEntMan.System<NpcFactionSystem>().AddFaction(hostile, EnemyFaction);
            var system = SEntMan.System<CMUQueenSwarmSystem>();
            system.Update(0);
            Assert.That(SEntMan.GetComponent<NPCMeleeCombatComponent>(lesser).Target, Is.EqualTo(hostile));

            if (passive)
                SEntMan.EventBus.RaiseLocalEvent(queen, new CMUSwarmSetPassiveEvent(SEntMan.GetNetEntity(hive), true));
            else
                SEntMan.EventBus.RaiseLocalEvent(queen, new CMUSwarmSetAllyEvent(SEntMan.GetNetEntity(hive), EnemyFaction, true));
            Assert.That(SEntMan.HasComponent<NPCMeleeCombatComponent>(lesser), Is.False, "Changing orders must cancel the current attack immediately.");
            var newcomer = SEntMan.SpawnEntity("CMUSwarmLesser", map.GridCoords);
            hives.SetHive(newcomer, hive);
            swarm.Members.Add(newcomer);
            system.Update(0);
            foreach (var member in swarm.Members)
            {
                Assert.That(SEntMan.HasComponent<NPCMeleeCombatComponent>(member), Is.False);
                Assert.That(SEntMan.GetComponent<NPCSteeringComponent>(member).Coordinates.EntityId, Is.EqualTo(swarm.Marker));
            }

            if (passive)
                SEntMan.EventBus.RaiseLocalEvent(queen, new CMUSwarmSetPassiveEvent(SEntMan.GetNetEntity(hive), false));
            else
                SEntMan.EventBus.RaiseLocalEvent(queen, new CMUSwarmSetAllyEvent(SEntMan.GetNetEntity(hive), EnemyFaction, false));
            system.Update(0);
            foreach (var member in swarm.Members)
                Assert.That(SEntMan.GetComponent<NPCMeleeCombatComponent>(member).Target, Is.EqualTo(hostile));
        });
    }

    [Test]
    public async Task SwarmSplitsAcrossVisibleEnemiesAndRegroupsWhenOneIsGone()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hives = SEntMan.System<SharedXenoHiveSystem>();
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var swarm = SEntMan.AddComponent<CMUSwarmControllerComponent>(hive);
            swarm.Anchor = SEntMan.SpawnEntity("CMUSwarmGuardMarker", map.GridCoords);
            for (var i = 0; i < 6; i++)
            {
                var lesser = SEntMan.SpawnEntity("CMUSwarmLesser", map.GridCoords);
                hives.SetHive(lesser, hive);
                swarm.Members.Add(lesser);
            }
            var enemies = new List<EntityUid>();
            for (var i = 0; i < 2; i++)
            {
                var enemy = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(2, i)));
                SEntMan.AddComponent<MobStateComponent>(enemy);
                SEntMan.System<NpcFactionSystem>().AddFaction(enemy, EnemyFaction);
                enemies.Add(enemy);
            }
            var system = SEntMan.System<CMUQueenSwarmSystem>();
            system.Update(0);
            var targets = swarm.Members.Select(member => SEntMan.GetComponent<NPCMeleeCombatComponent>(member).Target).Distinct();
            Assert.That(targets, Is.EquivalentTo(enemies), "Nearby enemies must both draw attackers instead of the entire swarm choosing the nearest one.");
            SEntMan.DeleteEntity(enemies[0]);
            swarm.NextThink = TimeSpan.Zero;
            system.Update(0);
            foreach (var member in swarm.Members)
                Assert.That(SEntMan.GetComponent<NPCMeleeCombatComponent>(member).Target, Is.EqualTo(enemies[1]));
        });
    }

    [Test]
    public async Task LesserAttacksBlockingBarricadeThenResumesPursuit()
    {
        var map = await Pair.CreateTestMap();
        EntityUid lesser = default;
        EntityUid barricade = default;
        EntityUid enemy = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 6; y++)
            {
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                if (x != 0 || y == -1 || y == 6)
                    SEntMan.SpawnEntity("WallSolid", map.GridCoords.Offset(new Vector2(x + 0.5f, y + 0.5f)));
            }
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var swarm = SEntMan.AddComponent<CMUSwarmControllerComponent>(hive);
            swarm.Anchor = SEntMan.SpawnEntity("CMUSwarmGuardMarker", map.GridCoords.Offset(new Vector2(0.5f, 4.5f)));
            lesser = SEntMan.SpawnEntity("CMUSwarmLesser", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            SEntMan.System<SharedXenoHiveSystem>().SetHive(lesser, hive);
            swarm.Members.Add(lesser);
            barricade = SEntMan.SpawnEntity("CMUTestSwarmBarbedBarricade", map.GridCoords.Offset(new Vector2(0.5f, 2.5f)));
            enemy = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(0.5f, 4.5f)));
            SEntMan.AddComponent<MobStateComponent>(enemy);
            SEntMan.System<NpcFactionSystem>().AddFaction(enemy, EnemyFaction);
        });
        await Pair.RunTicksSync(120);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(barricade).Float(), Is.GreaterThan(0),
                "An unclimbable barricade between the lesser and its enemy must take real melee damage.");
            SEntMan.DeleteEntity(barricade);
        });
        await Pair.RunTicksSync(120);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<NPCMeleeCombatComponent>(lesser).Target, Is.EqualTo(enemy));
            Assert.That(SEntMan.GetComponent<TransformComponent>(lesser).Coordinates.Position.Y, Is.GreaterThan(2.5f));
        });
    }

    [Test]
    public async Task OneSightedEnemyRecruitsSwarmThenNestingReturnsThemToGuard()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hiveSystem = SEntMan.System<SharedXenoHiveSystem>();
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            SEntMan.GetComponent<CMUQueenSwarmComponent>(queen).Enabled = true;
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

            // A previously acquired hostile becomes protected while its sighting is still fresh.
            SEntMan.AddComponent<XenoNestedComponent>(hostile);
            swarm.NextThink = TimeSpan.Zero;
            system.Update(0);
            Assert.That(swarm.Sightings, Does.Not.ContainKey(hostile));
            Assert.That(SEntMan.HasComponent<NPCMeleeCombatComponent>(scout), Is.False);
            Assert.That(SEntMan.HasComponent<NPCMeleeCombatComponent>(follower), Is.False);
            Assert.That(SEntMan.GetComponent<NPCSteeringComponent>(follower).Coordinates.EntityId, Is.EqualTo(swarm.Marker));
        });
    }
}
