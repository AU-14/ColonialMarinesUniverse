using Content.Shared._RMC14.Medical.CPR;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class CMUReportedDroneCPRTest
{
    [TestCase("CMUCombatDrone")]
    [TestCase("CMUFlamerDrone")]
    public async Task WreckedDroneRejectsCPRAtStartAndCompletion(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entities.SpawnEntity(prototype, map.GridCoords);
            entities.System<DamageableSystem>().TryChangeDamage(target,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 250 } }, true);
            Assert.That(entities.System<MobStateSystem>().IsDead(target), Is.True);

            var interaction = new InteractHandEvent(user, target);
            entities.EventBus.RaiseLocalEvent(target, interaction);
            Assert.That(entities.HasComponent<ReceivingCPRComponent>(target), Is.False,
                "Hand interaction must not start a CPR do-after on a wreck.");
            foreach (var start in new[] { true, false })
            {
                var attempt = new ReceiveCPRAttemptEvent(user, target, start);
                entities.EventBus.RaiseLocalEvent(target, ref attempt);
                Assert.That(attempt.Cancelled, Is.True);
            }

            var human = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.System<MobStateSystem>().ChangeMobState(human, Content.Shared.Mobs.MobState.Critical);
            var humanAttempt = new ReceiveCPRAttemptEvent(user, human, true);
            entities.EventBus.RaiseLocalEvent(human, ref humanAttempt);
            Assert.That(humanAttempt.Cancelled, Is.False, "Organic patients must remain eligible.");
        });
        await pair.CleanReturnAsync();
    }
}
