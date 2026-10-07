using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Construction.ResinHole;
using Content.Shared.Standing;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Xenos;

[TestFixture]
public sealed class ResinHoleLifetimeTest : GameTest
{
    [Test]
    public async Task DeletingTrapClearsItsReferenceWhileVictimIsStillDown()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var victim = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Server.System<StandingStateSystem>().Down(victim, playSound: false, dropHeldItems: false, force: true);
            var first = CreateTrap();
            var second = CreateTrap();
            var range = SEntMan.GetComponent<InResinHoleRangeComponent>(victim);
            Assert.That(range.HoleList, Is.EquivalentTo(new[] { first, second }));
            SEntMan.DeleteEntity(first);
            Assert.That(range.HoleList, Is.EqualTo(new[] { second }));
            Assert.DoesNotThrow(() => SEntMan.GetComponentState(SEntMan.EventBus, range, null, GameTick.Zero));
            SEntMan.DeleteEntity(second);
            Assert.That(range.HoleList, Is.Empty);
            SEntMan.DeleteEntity(victim);

            EntityUid CreateTrap()
            {
                var trap = SEntMan.SpawnEntity(null, map.GridCoords);
                var component = SEntMan.AddComponent<XenoResinHoleComponent>(trap);
                component.TrapPrototype = XenoResinHoleComponent.AcidPrototype;
                var attempt = new StepTriggerAttemptEvent { Source = trap, Tripper = victim };
                SEntMan.EventBus.RaiseLocalEvent(trap, ref attempt);
                Assert.That(attempt.Continue, Is.False);
                return trap;
            }
        });
    }
}
