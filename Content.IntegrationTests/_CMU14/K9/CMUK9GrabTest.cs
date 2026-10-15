using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.K9;
using Content.Shared._RMC14.K9.Components;
using Content.Shared._RMC14.K9.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.IntegrationTests.CMU14.K9;

[TestFixture]
public sealed class CMUK9GrabTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task RepairedK9RecoversFromLethalDamage()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var dog = SEntMan.SpawnEntity("AU14MobK9", map.GridCoords);
            var damage = Server.System<DamageableSystem>();
            damage.TryChangeDamage(dog, new DamageSpecifier { DamageDict = new() { ["Blunt"] = FixedPoint2.New(2000) } }, true);
            Assert.That(SEntMan.GetComponent<MobStateComponent>(dog).CurrentState, Is.EqualTo(MobState.Dead));
            damage.ClearAllDamage(dog);
            Assert.That(SEntMan.GetComponent<MobStateComponent>(dog).CurrentState, Is.EqualTo(MobState.Alive),
                "repairing lethal K9 damage must restore its living state");
        });
    }

    [Test]
    public async Task K9CanClimbOverAnObstacle()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var dog = SEntMan.SpawnEntity("AU14MobK9", map.GridCoords);
            var obstacle = SEntMan.SpawnEntity(null, map.GridCoords);
            var climbable = SEntMan.AddComponent<ClimbableComponent>(obstacle);
            Assert.That(Server.System<ClimbSystem>().TryClimb(dog, dog, obstacle, out _, climbable), Is.True);
        });
    }

    [Test]
    public async Task ReleasingArmGrabStopsPullingAndAllowsAnotherGrab()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var dog = SEntMan.SpawnEntity("AU14MobK9", map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var grab = new K9ArmGrabActionEvent { Performer = dog, Target = human };
            SEntMan.EventBus.RaiseLocalEvent(dog, grab);
            var pullable = SEntMan.GetComponent<PullableComponent>(human);
            Assert.That(pullable.Puller, Is.EqualTo(dog));

            Server.System<K9System>().ReleaseGrab(human);
            Assert.That(pullable.Puller, Is.Null, "releasing the grip must also stop the pull");

            var again = new K9ArmGrabActionEvent { Performer = dog, Target = human };
            SEntMan.EventBus.RaiseLocalEvent(dog, again);
            Assert.That(SEntMan.GetComponent<K9DogComponent>(dog).GrabbedTarget, Is.EqualTo(human),
                "re-grabbing in the same tick must establish a new grip instead of tripping a stale grab");
            Server.System<K9System>().ReleaseGrab(human);
        });
    }
}
