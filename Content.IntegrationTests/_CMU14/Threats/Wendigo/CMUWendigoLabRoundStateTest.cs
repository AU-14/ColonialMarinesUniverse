using Content.Server.CMU14.Threats.Rules;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Lab;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

[TestFixture]
public sealed class CMUWendigoLabRoundStateTest
{
    private static readonly Robust.Shared.Prototypes.EntProtoId KillAllXenoRulePrototype = "KillAllXenoRule";

    [Test, CancelAfter(180000)]
    public async Task LabMadeWendigoIsExcludedAndNeverEndsKillAllXeno()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ticker = entities.System<GameTicker>();
            var mobs = entities.System<MobStateSystem>();
            var rules = entities.System<ThreatRuleHelper>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            var wendigo = entities.SpawnEntity("AU14Wendigo", map.GridCoords);
            entities.AddComponent<CMUWendigoLabMadeComponent>(wendigo);
            var state = entities.GetComponent<MobStateComponent>(wendigo);
            Assert.That(rules.IsExcludedFromVictory(wendigo, state), Is.True);

            Assert.That(ticker.StartGameRule(KillAllXenoRulePrototype), Is.True);

            // As the only Xeno, an unmarked dead Wendigo would be 1/1 eliminated and end the round.
            mobs.ChangeMobState(wendigo, MobState.Dead);
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound),
                "A lab-made Wendigo must not count toward KillAllXeno totals.");
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The natural Wendigo is not a xeno, but it is still the threat KillAllXeno (the ColonyFall colonist win) ends on.
    /// </summary>
    [Test, CancelAfter(180000)]
    public async Task NaturalWendigoDeathEndsKillAllXeno()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ticker = entities.System<GameTicker>();
            var mobs = entities.System<MobStateSystem>();
            var rules = entities.System<ThreatRuleHelper>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            var wendigo = entities.SpawnEntity("AU14Wendigo", map.GridCoords);
            Assert.That(entities.HasComponent<XenoComponent>(wendigo), Is.False);
            var state = entities.GetComponent<MobStateComponent>(wendigo);
            Assert.That(rules.IsExcludedFromVictory(wendigo, state), Is.True,
                "The Wendigo is the threat, never a colonist survivor.");

            Assert.That(ticker.StartGameRule(KillAllXenoRulePrototype), Is.True);
            mobs.ChangeMobState(wendigo, MobState.Dead);
            Assert.That(ticker.RunLevel, Is.Not.EqualTo(GameRunLevel.InRound),
                "Killing the natural Wendigo must end the round for the colonists.");
        });
        await pair.CleanReturnAsync();
    }
}
