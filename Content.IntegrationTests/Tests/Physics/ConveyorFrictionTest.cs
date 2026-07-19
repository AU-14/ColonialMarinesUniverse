#nullable enable

using System.Numerics;
using Content.Shared.Conveyor;
using Content.Shared.Movement.Events;
using Content.Shared.Physics.Controllers;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;
using ServerConveyorController = Content.Server.Physics.Controllers.ConveyorController;

namespace Content.IntegrationTests.Tests.Physics;

[TestFixture]
[TestOf(typeof(SharedConveyorController))]
public sealed class ConveyorFrictionTest
{
    private const float InitialAngularVelocity = 5f;
    private const float FrameTime = 0.1f;

    [Test]
    public async Task FrictionOnlyAppliesWhileGroundedAndConveying()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var controller = server.System<ServerConveyorController>();
        var physics = server.System<PhysicsSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ground = entMan.SpawnEntity("Crowbar", map.GridCoords);
            var groundBody = entMan.GetComponent<PhysicsComponent>(ground);
            var groundConveyed = entMan.EnsureComponent<ConveyedComponent>(ground);

            groundConveyed.Conveying = false;
            var inactiveFriction = new TileFrictionEvent(0.75f);
            entMan.EventBus.RaiseLocalEvent(ground, ref inactiveFriction);
            Assert.That(inactiveFriction.Modifier, Is.EqualTo(0.75f));

            groundConveyed.Conveying = true;
            var activeFriction = new TileFrictionEvent(0.75f);
            entMan.EventBus.RaiseLocalEvent(ground, ref activeFriction);
            Assert.That(activeFriction.Modifier, Is.Zero);

            physics.SetBodyStatus(ground, groundBody, BodyStatus.OnGround);
            Assert.That(
                physics.SetAngularVelocity(ground, InitialAngularVelocity, body: groundBody),
                Is.True);

            var airborne = entMan.SpawnEntity(
                "Crowbar",
                map.GridCoords.Offset(Vector2.UnitX));
            var airborneBody = entMan.GetComponent<PhysicsComponent>(airborne);
            var airborneConveyed = entMan.EnsureComponent<ConveyedComponent>(airborne);
            airborneConveyed.Conveying = true;

            physics.SetBodyStatus(airborne, airborneBody, BodyStatus.InAir);
            Assert.That(
                physics.SetAngularVelocity(airborne, InitialAngularVelocity, body: airborneBody),
                Is.True);

            controller.UpdateBeforeSolve(prediction: false, frameTime: FrameTime);

            Assert.Multiple(() =>
            {
                Assert.That(
                    groundBody.AngularVelocity,
                    Is.LessThan(InitialAngularVelocity),
                    "Grounded conveyed bodies should stop spinning.");
                Assert.That(
                    airborneBody.AngularVelocity,
                    Is.EqualTo(InitialAngularVelocity),
                    "Airborne bodies must not receive conveyor friction.");
                Assert.That(
                    airborneConveyed.Conveying,
                    Is.False,
                    "Leaving the ground must clear active conveying.");
            });
        });

        await pair.CleanReturnAsync();
    }
}
