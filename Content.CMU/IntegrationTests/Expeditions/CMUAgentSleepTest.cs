using Content.IntegrationTests.Fixtures;
using Content.Shared.Bed.Sleep;
using Content.Shared.CCVar;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.Expeditions;

[TestFixture]
public sealed class CMUAgentSleepTest : GameTest
{
    [Test]
    public async Task UncontrolledAgentStaysAwakeAfterSsdTimeout()
    {
        var map = await Pair.CreateTestMap();
        EntityUid agent = default, disconnectedHuman = default;
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        var oldDelay = cfg.GetCVar(CCVars.ICSSDSleepTime);
        var oldEnabled = cfg.GetCVar(CCVars.ICSSDSleep);
        try
        {
            await Server.WaitAssertion(() =>
            {
                cfg.SetCVar(CCVars.ICSSDSleep, true);
                cfg.SetCVar(CCVars.ICSSDSleepTime, 1f);
                agent = SEntMan.SpawnEntity("CMUExpeditionScavenger", map.GridCoords);
                disconnectedHuman = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            });
            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<SleepingComponent>(disconnectedHuman), Is.True,
                    "The native disconnect timeout must still put ordinary humans to sleep.");
                Assert.That(SEntMan.HasComponent<SleepingComponent>(agent), Is.False,
                    "An AI-owned human is not a disconnected player.");
                var status = Server.System<StatusEffectsSystem>();
                Assert.That(status.HasStatusEffect(agent, SSDIndicatorSystem.StatusEffectSSDSleeping), Is.False);
                Assert.That(status.TrySetStatusEffectDuration(agent, SleepingSystem.StatusEffectForcedSleeping), Is.True);
                Assert.That(SEntMan.HasComponent<SleepingComponent>(agent), Is.True,
                    "Preventing SSD sleep must not grant immunity to actual forced sleep.");
            });
            await Pair.RunSeconds(1);
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.HasComponent<SleepingComponent>(agent), Is.True));
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                cfg.SetCVar(CCVars.ICSSDSleepTime, oldDelay);
                cfg.SetCVar(CCVars.ICSSDSleep, oldEnabled);
            });
        }
    }
}
