using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.K9.Components;
using Content.Shared._RMC14.K9.Events;
using Content.Shared.ActionBlocker;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Repairable;
using Content.Shared.Weapons.Melee;

namespace Content.IntegrationTests.CMU14.K9;

// bug report 854 regressions
[TestFixture]
public sealed class CMUK9ReportFixesTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase(MobState.Critical)]
    [TestCase(MobState.Dead)]
    public async Task VictimGoingDownReleasesTheGrip(MobState state)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var dog = SEntMan.SpawnEntity("AU14MobK9", map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(dog, new K9ArmGrabActionEvent { Performer = dog, Target = human });
            Assert.That(SEntMan.HasComponent<K9GrabbedComponent>(human), Is.True);

            Server.System<MobStateSystem>().ChangeMobState(human, state);

            Assert.That(SEntMan.HasComponent<K9GrabbedComponent>(human), Is.False);
            Assert.That(SEntMan.GetComponent<PullableComponent>(human).Puller, Is.Null);
            Assert.That(SEntMan.GetComponent<K9DogComponent>(dog).GrabbedTarget, Is.Null);
        });
    }

    [Test]
    public async Task DogGoingCritReleasesTheGrip()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var dog = SEntMan.SpawnEntity("AU14MobK9", map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(dog, new K9ArmGrabActionEvent { Performer = dog, Target = human });

            Server.System<MobStateSystem>().ChangeMobState(dog, MobState.Critical);

            Assert.That(SEntMan.HasComponent<K9GrabbedComponent>(human), Is.False);
            Assert.That(SEntMan.GetComponent<PullableComponent>(human).Puller, Is.Null);
        });
    }

}
