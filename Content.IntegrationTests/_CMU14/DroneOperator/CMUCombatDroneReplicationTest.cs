using System.Numerics;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Timing;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class CMUCombatDroneReplicationTest
{
    [Test]
    public async Task ApplyingMovementStateDoesNotOverwriteHullOrTurretTransforms()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        NetEntity droneNet = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var drone = entities.SpawnEntity("CMUCombatDrone", map.GridCoords);
            var component = entities.GetComponent<CMUCombatDroneComponent>(drone);
            Assert.That(component.TurretVisual, Is.Not.Null, "The gun UGV must spawn its independently rotating turret.");
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, drone);
            droneNet = entities.GetNetEntity(drone);
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var drone = entities.GetEntity(droneNet);
            var turret = entities.GetComponent<CMUCombatDroneComponent>(drone).TurretVisual!.Value;
            var transform = entities.System<TransformSystem>();
            var input = entities.GetComponent<InputMoverComponent>(drone);
            var hullTransform = entities.GetComponent<TransformComponent>(drone);
            var turretTransform = entities.GetComponent<TransformComponent>(turret);
            transform.Reset();
            transform.SetLocalRotationNoLerp(drone, Angle.Zero);
            transform.SetLocalRotationNoLerp(turret, Angle.FromDegrees(45));

            // Reproduce the reported ordering: transforms begin interpolation, then
            // InputMover receives a different button state in the same snapshot.
            using (pair.Client.ResolveDependency<IClientGameTiming>().StartStateApplicationArea())
            {
                transform.ActivateLerp(drone, hullTransform);
                transform.ActivateLerp(turret, turretTransform);
                var state = new ComponentHandleState(new InputMoverComponentState
                {
                    HeldMoveButtons = MoveButtons.Right,
                    CanMove = true,
                }, null);
                entities.EventBus.RaiseComponentEvent(drone, input, ref state);
                Assert.That(input.HeldMoveButtons, Is.EqualTo(MoveButtons.Right));
                Assert.That(hullTransform.LocalRotation, Is.EqualTo(Angle.Zero));
                Assert.That(turretTransform.LocalRotation, Is.EqualTo(Angle.FromDegrees(45)));
            }

            // Ordinary predicted input still turns the hull and recenters its turret.
            input.HeldMoveButtons = MoveButtons.Left;
            var move = new MoveInputEvent((drone, input), MoveButtons.Right);
            entities.EventBus.RaiseLocalEvent(drone, ref move);
            Assert.That(transform.GetWorldRotation(drone).GetCardinalDir(), Is.EqualTo(Direction.West));
            Assert.That(turretTransform.LocalRotation, Is.EqualTo(Angle.Zero));
            input.HeldMoveButtons = MoveButtons.None;
            transform.Reset();
        });
        await pair.CleanReturnAsync();
    }
}
