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

    private static readonly Robust.Shared.Prototypes.EntProtoId K9Proto = "AU14MobK9";

    [Test]
    public async Task DogDroppingThePullLetsGo()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var dog = SEntMan.SpawnEntity("AU14MobK9", map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(dog, new K9ArmGrabActionEvent { Performer = dog, Target = human });
            var pullable = SEntMan.GetComponent<PullableComponent>(human);
            Assert.That(pullable.Puller, Is.EqualTo(dog));

            // same path as the dog hitting its stop-pulling key
            Server.System<PullingSystem>().TryStopPull(human, pullable, dog);

            Assert.That(pullable.Puller, Is.Null, "the grip used to re-pull straight after the dog let go");
            Assert.That(SEntMan.HasComponent<K9GrabbedComponent>(human), Is.False);
            Assert.That(SEntMan.GetComponent<K9DogComponent>(dog).GrabbedTarget, Is.Null);
        });
    }

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

    [TestCase("XenoWeeds")]
    [TestCase("XenoWeedsSource")]
    [TestCase("XenoWeedsWall")]
    public async Task DogCanBiteWeeds(string weedsProto)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var dog = SEntMan.SpawnEntity("AU14MobK9", map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var weeds = SEntMan.SpawnEntity(weedsProto, map.GridCoords);
            var blocker = Server.System<ActionBlockerSystem>();

            var bite = SEntMan.GetComponent<MeleeWeaponComponent>(dog);
            Assert.That(blocker.CanAttack(dog, weeds, (dog, bite)), Is.True);

            // bare-handed humans are still kept off weeds
            if (SEntMan.TryGetComponent(human, out MeleeWeaponComponent? fists))
                Assert.That(blocker.CanAttack(human, weeds, (human, fists)), Is.False);
        });
    }

    [Test]
    public async Task DogDoesNotReadXenoNames()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var dog = SEntMan.SpawnEntity("AU14MobK9", map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);

            var realName = SEntMan.GetComponent<MetaDataComponent>(xeno).EntityName;
            var humanSees = Identity.Name(xeno, SEntMan, human);
            var dogSees = Identity.Name(xeno, SEntMan, dog);

            Assert.That(humanSees.Name, Is.Not.EqualTo(realName), "sanity: humans get the masked name");
            Assert.That(dogSees.Name, Is.EqualTo(humanSees.Name));
        });
    }

    [Test]
    public async Task DogHasNoInstantWelderRepair()
    {
        await Server.WaitAssertion(() =>
        {
            // Repairable welds every damage type off in a second; synth repair is the doafter'd one
            var proto = SProtoMan.Index(K9Proto);
            Assert.That(proto.Components.ContainsKey(SEntMan.ComponentFactory.GetComponentName<RepairableComponent>()), Is.False);
        });
    }
}
