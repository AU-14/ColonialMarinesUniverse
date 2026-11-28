using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Gibbing;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUXenoCorpseDragTest : GameTest
{
    [Test]
    public async Task MovingPullerCannotTakeAHeavyCorpseButKeepsTheirGripOnStairs()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var holder = SEntMan.SpawnAtPosition("CMMobHuman", map.GridCoords);
            var runner = SEntMan.SpawnAtPosition("CMMobHuman", map.GridCoords);
            var corpse = SEntMan.SpawnAtPosition("CMXenoRavager", map.GridCoords.Offset(Vector2.UnitX));
            SEntMan.RemoveComponent<RMCGibOnDeathComponent>(corpse);
            Server.System<MobStateSystem>().ChangeMobState(corpse, MobState.Dead);
            var pulling = Server.System<PullingSystem>();
            var pulled = SEntMan.GetComponent<PullableComponent>(corpse);
            Assert.That(pulling.TryStartPull(holder, corpse), Is.True);

            var movement = Server.System<SharedMoverController>();
            var mover = SEntMan.GetComponent<InputMoverComponent>(runner);
            movement.SetVelocityDirection((runner, mover), Direction.East, 0, true);
            Assert.That(pulling.TogglePull((corpse, pulled), runner), Is.False);
            Assert.That(pulled.Puller, Is.EqualTo(holder), "A rejected grab must preserve the existing grip.");
            movement.SetVelocityDirection((runner, mover), Direction.East, 0, false);
            Assert.That(pulling.TogglePull((corpse, pulled), runner), Is.True);

            movement.SetVelocityDirection((runner, mover), Direction.East, 0, true);
            var levels = Server.System<CMUZLevelsSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var landing = transform.ToMapCoordinates(map.GridCoords.Offset(Vector2.UnitY));
            Assert.That(levels.TryPreparePulledEntityZMove(runner, transform.GetMapCoordinates(runner), out var transfer), Is.True);
            transform.SetMapCoordinates(runner, landing);
            levels.MovePulledEntityAcrossZ(transfer, landing, 0f);
            Assert.That(pulled.Puller, Is.EqualTo(runner), "Stairs must restore the grip even while the movement key is held.");
            movement.SetVelocityDirection((runner, mover), Direction.East, 0, false);
        });
    }
}
