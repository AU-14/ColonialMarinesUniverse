using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Gravity;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Gravity;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.IntegrationTests.Tests.Doors;

[TestFixture]
[TestOf(typeof(SharedDoorSystem))]
public sealed class DoorMergeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
    };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: DoorMergeHive
          components:
          - type: Hive

        - type: entity
          parent: MobHuman
          id: DoorMergeUser

        - type: entity
          parent: DoorMergeUser
          id: DoorMergeCultist
          components:
          - type: Cultist

        - type: entity
          id: DoorMergeDoor
          components:
          - type: Appearance
          - type: Physics
            bodyType: Static
          - type: Fixtures
            fixtures:
              door:
                shape:
                  !type:PhysShapeAabb
                  bounds: "-0.49,-0.49,0.49,0.49"
                mask:
                - FullTileMask
                layer:
                - AirlockLayer
          - type: Door
            state: Open
            openTimeOne: 1
            doorStunTime: 5
            checkFixtureCollision: true

        - type: entity
          parent: DoorMergeDoor
          id: DoorMergeWindoor
          components:
          - type: Door
            allowMachineLayer: true

        - type: entity
          id: DoorMergeMachine
          components:
          - type: Physics
            bodyType: Dynamic
          - type: Fixtures
            fixtures:
              machine:
                shape:
                  !type:PhysShapeCircle
                  radius: 0.3
                mask:
                - LowImpassable
                layer:
                - MachineLayer

        - type: entity
          id: DoorMergeDropshipObstacle
          components:
          - type: Physics
            bodyType: Dynamic
          - type: Fixtures
            fixtures:
              obstacle:
                shape:
                  !type:PhysShapeCircle
                  radius: 0.3
                mask:
                - LowImpassable
                layer:
                - DropshipImpassable
        """;

    [Test]
    public async Task PausedResinUserAndMachineLayerClosingRulesRemainDistinct()
    {
        var map = await Pair.CreateTestMap();
        var entities = new List<EntityUid>();

        try
        {
            EntityUid resinDoor = default;
            EntityUid hive = default;
            EntityUid ally = default;
            EntityUid outsider = default;
            EntityUid cultist = default;

            await Server.WaitAssertion(() =>
            {
                var doors = Server.System<SharedDoorSystem>();
                var hives = Server.System<SharedXenoHiveSystem>();
                resinDoor = Spawn("CMU14XenoMyceliumDoor", map.GridCoords, entities);
                hive = Spawn("DoorMergeHive", map.GridCoords.Offset(new Vector2(2, 0)), entities);
                ally = Spawn("DoorMergeUser", map.GridCoords.Offset(new Vector2(2, 0)), entities);
                outsider = Spawn("DoorMergeUser", map.GridCoords.Offset(new Vector2(2, 0)), entities);
                cultist = Spawn("DoorMergeCultist", map.GridCoords.Offset(new Vector2(2, 0)), entities);

                hives.SetHive(resinDoor, hive);
                hives.SetHive(ally, hive);
                var door = SEntMan.GetComponent<DoorComponent>(resinDoor);
                doors.SetState(resinDoor, DoorState.Open, door);

                Assert.Multiple(() =>
                {
                    Assert.That(doors.CanClose(resinDoor, door, outsider), Is.False,
                        "BeforeDoorClosedEvent must retain the non-ally user for the CMU resin-door gate");
                    Assert.That(doors.CanClose(resinDoor, door, ally), Is.True,
                        "a member of the resin door's hive may close it");
                    Assert.That(doors.CanClose(resinDoor, door, cultist), Is.True,
                        "Cultist remains the explicit resin-door access exception");
                });
            });

            await Delete(resinDoor, hive, ally, outsider, cultist);
            entities.Remove(resinDoor);
            entities.Remove(hive);
            entities.Remove(ally);
            entities.Remove(outsider);
            entities.Remove(cultist);

            EntityUid ordinary = default;
            EntityUid machine = default;
            await Server.WaitPost(() =>
            {
                ordinary = Spawn("DoorMergeDoor", map.GridCoords, entities);
                machine = Spawn("DoorMergeMachine", map.GridCoords, entities);
            });
            await Pair.RunTicksSync(2);

            await Server.WaitAssertion(() =>
            {
                var doors = Server.System<SharedDoorSystem>();
                var meta = Server.System<MetaDataSystem>();
                var door = SEntMan.GetComponent<DoorComponent>(ordinary);

                Assert.That(doors.CanClose(ordinary, door), Is.False,
                    "an ordinary door must treat an overlapping MachineLayer fixture as an obstruction");
                meta.SetEntityPaused(ordinary, true);
                Assert.That(doors.CanClose(ordinary, door), Is.False,
                    "paused doors must reject closing before access and collision checks");
                meta.SetEntityPaused(ordinary, false);
            });

            await Delete(ordinary);
            entities.Remove(ordinary);

            EntityUid windoor = default;
            await Server.WaitPost(() => windoor = Spawn("DoorMergeWindoor", map.GridCoords, entities));
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                var doors = Server.System<SharedDoorSystem>();
                var door = SEntMan.GetComponent<DoorComponent>(windoor);
                Assert.Multiple(() =>
                {
                    Assert.That(door.CheckFixtureCollision, Is.True);
                    Assert.That(door.AllowMachineLayer, Is.True);
                    Assert.That(doors.CanClose(windoor, door), Is.True,
                        "only the windoor-style opt-in may close over MachineLayer");
                });
            });

            await Delete(windoor, machine);
            entities.Remove(windoor);
            entities.Remove(machine);

            EntityUid dropshipDoor = default;
            EntityUid dropshipObstacle = default;
            await Server.WaitPost(() =>
            {
                dropshipDoor = Spawn("DoorMergeDoor", map.GridCoords, entities);
                dropshipObstacle = Spawn("DoorMergeDropshipObstacle", map.GridCoords, entities);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                var doors = Server.System<SharedDoorSystem>();
                Assert.That(doors.CanClose(dropshipDoor), Is.True,
                    "DropshipImpassable fixtures are never ordinary door obstructions");
            });
        }
        finally
        {
            await Delete(entities.ToArray());
        }
    }

    [Test]
    public async Task CrushingPrunesDeletedTargetsAndReplicatesSuccessorParalysisState()
    {
        var map = await Pair.CreateTestMap();
        var entities = new List<EntityUid>();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid observer = default;
        EntityUid door = default;
        EntityUid target = default;
        NetEntity doorNet = default;

        try
        {
            await Server.WaitPost(() =>
            {
                observer = Spawn("DoorMergeUser", map.GridCoords.Offset(new Vector2(2, 0)), entities);
                door = Spawn("DoorMergeDoor", map.GridCoords, entities);
                target = Spawn("MobHuman", map.GridCoords, entities);
                var gravity = SEntMan.EnsureComponent<GravityComponent>(map.Grid.Owner);
                Server.System<GravitySystem>().EnableGravity(map.Grid.Owner, gravity);
                SEntMan.RemoveComponent<StatusEffectsComponent>(target);
                doorNet = SEntMan.GetNetEntity(door);
                Server.PlayerMan.SetAttachedEntity(session, observer);
            });
            await Pair.RunTicksSync(3);

            await Server.WaitAssertion(() =>
            {
                var doors = Server.System<SharedDoorSystem>();
                var statuses = Server.System<NewStatusEffectsSystem>();
                var doorComp = SEntMan.GetComponent<DoorComponent>(door);
                Assert.That(SEntMan.GetComponent<GravityAffectedComponent>(target).Weightless, Is.False,
                    "door paralysis requires the representative mob to be standing in gravity");
                doors.SetState(door, DoorState.Closing, doorComp);
                var before = Server.Timing.CurTime;
                doors.Crush(door, doorComp, SEntMan.GetComponent<PhysicsComponent>(door));

                Assert.Multiple(() =>
                {
                    Assert.That(doorComp.CurrentlyCrushing, Is.EquivalentTo(new[] { target }));
                    Assert.That(doorComp.IsCrushing, Is.True);
                    Assert.That(statuses.TryGetTime(target, SharedStunSystem.ParalyzeId, out var paralysis), Is.True,
                        "door crushing must use the successor paralysis status");
                    Assert.That(paralysis.EndEffectTime, Is.EqualTo(before + TimeSpan.FromSeconds(6)));
                    Assert.That(SEntMan.HasComponent<StatusEffectsComponent>(target), Is.False,
                        "door crushing must not recreate the legacy status-effects component");
                });
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientDoor = CEntMan.GetEntity(doorNet);
                Assert.That(CEntMan.GetComponent<DoorComponent>(clientDoor).IsCrushing, Is.True,
                    "the live crushing state must be visible to clients");
            });

            await Delete(target);
            entities.Remove(target);
            await Server.WaitAssertion(() =>
            {
                var doors = Server.System<SharedDoorSystem>();
                var doorComp = SEntMan.GetComponent<DoorComponent>(door);
                doors.Crush(door, doorComp, SEntMan.GetComponent<PhysicsComponent>(door));
                Assert.Multiple(() =>
                {
                    Assert.That(doorComp.CurrentlyCrushing, Is.Empty,
                        "terminating or deleted targets must be pruned from the owned crushing set");
                    Assert.That(doorComp.IsCrushing, Is.False);
                });
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientDoor = CEntMan.GetEntity(doorNet);
                Assert.That(CEntMan.GetComponent<DoorComponent>(clientDoor).IsCrushing, Is.False,
                    "client crushing state must clear after authoritative pruning");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
            await Delete(entities.ToArray());
        }
    }

    private EntityUid Spawn(string prototype, EntityCoordinates coordinates, ICollection<EntityUid> entities)
    {
        var uid = SEntMan.SpawnEntity(prototype, coordinates);
        entities.Add(uid);
        return uid;
    }

    private async Task Delete(params EntityUid[] entities)
    {
        foreach (var uid in entities)
            await Pair.DeleteEntityTreeLeafFirst(uid);
    }
}
