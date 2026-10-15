using Content.Server.Movement.Components;
using Content.Server.Movement.Systems;
using Content.Shared.Input;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Input;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class PullCoordinateRegressionTest
{
    [Test]
    public async Task MovingBodyAtPullDestinationSlowsWithoutInvalidVelocity()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var puller = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var patient = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(Vector2.UnitX));
            Assert.That(entities.System<PullingSystem>().TryStartPull(puller, patient), Is.True);
            entities.System<SharedPhysicsSystem>().SetLinearVelocity(patient, Vector2.UnitX);
            entities.EnsureComponent<PullMovingComponent>(patient).MovingTo = entities.GetComponent<TransformComponent>(patient).Coordinates;

            entities.System<PullController>().UpdateBeforeSolve(false, 1f / 30f);

            var velocity = entities.GetComponent<PhysicsComponent>(patient).LinearVelocity;
            Assert.That(float.IsFinite(velocity.X) && float.IsFinite(velocity.Y), Is.True,
                "Settling a moving patient at the requested destination must not create NaN physics.");
            Assert.That(velocity.Length(), Is.LessThan(1f));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PullClickFromPreviousMapIsIgnored()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var otherMap = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var puller = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var patient = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(Vector2.UnitX));
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, puller);
            Assert.That(entities.System<PullingSystem>().TryStartPull(puller, patient), Is.True);
            var message = new FullInputCmdMessage(GameTick.Zero, 0, 0, default, BoundKeyState.Down,
                entities.GetNetCoordinates(otherMap.GridCoords), ScreenCoordinates.Invalid);
            foreach (var handler in entities.System<SharedInputSystem>().BindRegistry.GetHandlers(ContentKeyFunctions.MovePulledObject))
                handler.HandleCmdMessage(entities, pair.Player, message);
            Assert.That(entities.HasComponent<PullMovingComponent>(patient), Is.False,
                "An in-flight click from the old deck cannot steer a patient on the new map.");
        });
        await pair.CleanReturnAsync();
    }
}
