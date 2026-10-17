using Content.IntegrationTests.Fixtures;
using Content.Server.Physics.Controllers;
using Content.Shared.CMU14.TileMovement;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class TileMovementFiniteStateTest : GameTest
{
    [TestCase(float.NaN, false)]
    [TestCase(float.NaN, true)]
    [TestCase(float.PositiveInfinity, false)]
    [TestCase(float.PositiveInfinity, true)]
    [TestCase(float.NegativeInfinity, false)]
    [TestCase(float.NegativeInfinity, true)]
    [TestCase(float.MaxValue, false)]
    [TestCase(float.MaxValue, true)]
    public async Task InvalidSlideCoordinatesStopAndCanResume(float coordinate, bool corruptOrigin)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f)));
            try
            {
                var mover = Server.System<MoverController>();
                var input = SComp<InputMoverComponent>(uid);
                var physics = SComp<PhysicsComponent>(uid);
                var transform = SComp<TransformComponent>(uid);
                var tile = SEntMan.EnsureComponent<CMUTileMovementComponent>(uid);
                var position = transform.LocalPosition;
                input.HeldMoveButtons = MoveButtons.Right;
                Tick();
                Assert.That(tile.SlideActive, Is.True);

                if (corruptOrigin)
                    tile.Origin = new EntityCoordinates(transform.ParentUid, new Vector2(coordinate, position.Y));
                else
                    tile.Destination = new Vector2(position.X, coordinate);

                tile.FailureSlideActive = true;
                Tick();
                Assert.Multiple(() =>
                {
                    Assert.That(tile.SlideActive, Is.False);
                    Assert.That(tile.FailureSlideActive, Is.False);
                    Assert.That(tile.LastTickLocalCoordinates, Is.Null);
                    Assert.That(physics.LinearVelocity, Is.EqualTo(Vector2.Zero));
                    Assert.That(transform.LocalPosition, Is.EqualTo(position));
                });

                Tick();
                Assert.That(tile.SlideActive, Is.True);
                Assert.That(physics.LinearVelocity.X, Is.GreaterThan(0));
                Assert.That(float.IsFinite(physics.LinearVelocity.X) && float.IsFinite(physics.LinearVelocity.Y), Is.True);

                void Tick()
                {
                    Assert.DoesNotThrow(() => mover.HandleTileMovement(uid, uid, tile, physics, transform,
                        input, null, null, 1f / 30f));
                }
            }
            finally
            {
                SEntMan.DeleteEntity(uid);
            }
        });
        await Pair.DeleteEntityTreeLeafFirst(map.Grid);
    }

    [TestCase(0f, false)]
    [TestCase(0f, true)]
    [TestCase(-1f, false)]
    [TestCase(-1f, true)]
    [TestCase(float.NaN, false)]
    [TestCase(float.NaN, true)]
    [TestCase(float.PositiveInfinity, false)]
    [TestCase(float.PositiveInfinity, true)]
    [TestCase(float.NegativeInfinity, false)]
    [TestCase(float.NegativeInfinity, true)]
    [TestCase(float.Epsilon, false)]
    public async Task SlideWithDegenerateSpeedStaysFiniteAndCanResume(float speed, bool alreadySliding)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f)));
            try
            {
                var mover = Server.System<MoverController>();
                var modifiers = Server.System<MovementSpeedModifierSystem>();
                var input = SComp<InputMoverComponent>(uid);
                var physics = SComp<PhysicsComponent>(uid);
                var transform = SComp<TransformComponent>(uid);
                var tile = SEntMan.EnsureComponent<CMUTileMovementComponent>(uid);
                if (alreadySliding)
                {
                    input.HeldMoveButtons = MoveButtons.Right;
                    Tick();
                    Assert.That(tile.SlideActive, Is.True);
                    Assert.That(physics.LinearVelocity.X, Is.GreaterThan(0));
                }

                input.HeldMoveButtons = MoveButtons.None;
                modifiers.ChangeBaseSpeed(uid, speed, speed, 20);

                // Landing at the tile center starts a zero-distance slide. Losing movement
                // speed during this slide used to produce infinity * zero in its timeout.
                tile.WasWeightlessLastTick = !alreadySliding;
                Tick();
                tile.WasWeightlessLastTick = false;
                Tick();
                Assert.That(physics.LinearVelocity, Is.EqualTo(Vector2.Zero));
                Assert.That(tile.SlideActive, Is.False);

                // An idle entity must still respond to external pushes even when its
                // own movement speed is zero. Leave that velocity to normal friction.
                Server.System<SharedPhysicsSystem>().SetLinearVelocity(uid, new Vector2(10, 0));
                Tick();
                Assert.That(physics.LinearVelocity.X, Is.GreaterThan(0));
                Assert.That(physics.LinearVelocity.X, Is.LessThan(10));

                modifiers.ChangeBaseSpeed(uid, 2.5f, 4.5f, 20);
                input.HeldMoveButtons = MoveButtons.Right;
                Tick();
                Assert.That(float.IsFinite(physics.LinearVelocity.X) && float.IsFinite(physics.LinearVelocity.Y), Is.True);
                Assert.That(physics.LinearVelocity.X, Is.GreaterThan(0));
                Assert.That(tile.SlideActive, Is.True);

                void Tick()
                {
                    Assert.DoesNotThrow(() => mover.HandleTileMovement(uid, uid, tile, physics, transform,
                        input, null, null, 1f / 30f));
                }
            }
            finally
            {
                SEntMan.DeleteEntity(uid);
            }
        });
        await Pair.DeleteEntityTreeLeafFirst(map.Grid);
    }
}
