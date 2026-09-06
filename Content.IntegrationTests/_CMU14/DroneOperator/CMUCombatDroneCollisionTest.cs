using System.Numerics;
using Content.Server.Gravity;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.Timing;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class CMUCombatDroneCollisionTest
{
    [TestCase("CMUCombatDrone", "CMWallMetal")]
    [TestCase("CMUFlamerDrone", "CMWallMetal")]
    [TestCase("CMUCombatDrone", "CMBarricadeMetal")]
    [TestCase("CMUFlamerDrone", "CMBarricadeMetal")]
    [TestCase("CMUCombatDrone", "WallXenoResin")]
    [TestCase("CMUFlamerDrone", "WallXenoResin")]
    [TestCase("CMUCombatDrone", "WallXenoResinThick")]
    [TestCase("CMUFlamerDrone", "WallXenoResinThick")]
    public async Task PilotedMovementStopsAtObstaclesAndResumesWhenCleared(string prototype, string obstacle)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid drone = default;
        EntityUid wall = default;
        EntityUid mind = default;
        NetEntity droneNet = default;

        await server.WaitAssertion(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            for (var x = 0; x < 12; x++)
            {
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, 0), map.Tile.Tile);
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, 1), map.Tile.Tile);
            }
            var gravity = entities.EnsureComponent<GravityComponent>(map.Grid);
            entities.System<GravitySystem>().EnableGravity(map.Grid, gravity);

            drone = entities.SpawnEntity(prototype, map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            droneNet = entities.GetNetEntity(drone);
            wall = entities.SpawnEntity(obstacle, map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
            entities.System<SharedTransformSystem>().SetLocalRotation(wall, Angle.FromDegrees(-90));
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 1.5f)));
            entities.AddComponent<CMUDroneOperatorComponent>(user);
            var tablet = entities.SpawnEntity("CMUDroneControlTablet", map.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, tablet, checkActionBlocker: false), Is.True);
            var droneCoords = entities.GetComponent<TransformComponent>(drone).Coordinates;
            var link = new InteractUsingEvent(user, tablet, drone, droneCoords);
            entities.EventBus.RaiseLocalEvent(drone, link);
            var minds = entities.System<SharedMindSystem>();
            mind = minds.GetOrCreateMind(pair.Player!.UserId).Owner;
            minds.TransferTo(mind, user);
            entities.EventBus.RaiseLocalEvent(tablet, new UseInHandEvent(user));
            Assert.That(entities.GetComponent<MindComponent>(mind).VisitingEntity, Is.EqualTo(drone));
            var body = entities.GetComponent<PhysicsComponent>(drone);
            Assert.That(body.CanCollide, Is.True);
            Assert.That(body.Hard, Is.True);
        });

        await pair.RunUntilSynced();
        await SetMovement(BoundKeyState.Down);
        await pair.RunTicksSync(90);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<MindComponent>(mind).VisitingEntity, Is.EqualTo(drone));
            var position = entities.GetComponent<TransformComponent>(drone).LocalPosition;
            Assert.That(position.X, Is.GreaterThan(0.75f), "The drone must actually move toward the obstacle.");
            Assert.That(position.X, Is.LessThan(1.85f), "The drone's collider must stop before entering the obstacle.");
        });
        await pair.Client.WaitAssertion(() =>
        {
            var clientDrone = pair.Client.EntMan.GetEntity(droneNet);
            Assert.That(pair.Client.EntMan.GetComponent<TransformComponent>(clientDrone).LocalPosition.X,
                Is.LessThan(1.85f), "Client prediction must also respect the obstacle.");
        });
        await server.WaitPost(() => entities.DeleteEntity(wall));

        await pair.RunTicksSync(90);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<TransformComponent>(drone).LocalPosition.X, Is.GreaterThan(3f),
                "Removing the obstacle must let the drone continue through the cleared space.");
        });
        await SetMovement(BoundKeyState.Up);

        await pair.CleanReturnAsync();

        async Task SetMovement(BoundKeyState state)
        {
            await pair.Client.WaitPost(() =>
            {
                var input = pair.Client.ResolveDependency<IInputManager>();
                var timing = pair.Client.ResolveDependency<IClientGameTiming>();
                var player = pair.Client.ResolveDependency<Robust.Client.Player.IPlayerManager>();
                var clientDrone = pair.Client.EntMan.GetEntity(droneNet);
                var key = EngineKeyFunctions.MoveRight;
                var message = new ClientFullInputCmdMessage(timing.CurTick, timing.TickFraction,
                    input.NetworkBindMap.KeyFunctionID(key))
                {
                    State = state,
                    Coordinates = pair.Client.EntMan.GetComponent<TransformComponent>(clientDrone).Coordinates,
                };
                pair.Client.EntMan.System<InputSystem>().HandleInputCommand(player.LocalSession!, key, message);
            });
        }
    }
}
