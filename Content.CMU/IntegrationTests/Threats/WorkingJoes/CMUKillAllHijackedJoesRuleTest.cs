using Content.Server.CMU14.Threats.Mobs.WorkingJoe.Hijack;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;
using Content.Shared.Gibbing;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats.WorkingJoes;

/// <summary>
/// The colony's win condition against the Working Joes shipment: the round ends when every threat unit is dead,
/// deleted or off the planet. A unit on the departed, off-planet ship counts as eliminated; a cured unit leaves
/// the count; a gibbed last unit, which never goes Dead, still ends the round.
/// </summary>
[TestFixture]
public sealed class CMUKillAllHijackedJoesRuleTest
{
    private static readonly EntProtoId Rule = "CMUKillAllHijackedJoesRule";
    private const string HijackedJoe = "CMUMobHijackedJoe";

    [Test, CancelAfter(180000)]
    public async Task TheRoundEndsWhenTheLastUnitDiesOrIsGibbed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var ticker = entities.System<GameTicker>();
            var mobs = entities.System<MobStateSystem>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            entities.EnsureComponent<RMCPlanetComponent>(map.MapUid);

            var first = entities.SpawnEntity(HijackedJoe, map.GridCoords);
            var last = entities.SpawnEntity(HijackedJoe, map.GridCoords);
            Assert.That(ticker.StartGameRule(Rule), Is.True);

            mobs.ChangeMobState(first, MobState.Dead);
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound), "one unit is still alive on the planet");

            entities.System<GibbingSystem>().Gib(last);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(6));
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.PostRound),
                "with the last unit gibbed the shipment is gone");
        });

        await pair.CleanReturnAsync();
    }

    [Test, CancelAfter(180000)]
    public async Task AUnitOffThePlanetCountsAsEliminated()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Dirty = true });
        var server = pair.Server;
        var planet = await pair.CreateTestMap();
        var deepSpace = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var ticker = entities.System<GameTicker>();
            var mobs = entities.System<MobStateSystem>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            entities.EnsureComponent<RMCPlanetComponent>(planet.MapUid);

            var ashore = entities.SpawnEntity(HijackedJoe, planet.GridCoords);
            // left aboard the ship when it returned to deep space: alive, but out of the fight
            entities.SpawnEntity(HijackedJoe, deepSpace.GridCoords);
            Assert.That(ticker.StartGameRule(Rule), Is.True);

            mobs.ChangeMobState(ashore, MobState.Dead);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(6));
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.PostRound),
                "the unit stranded off the planet is eliminated");
        });

        await pair.CleanReturnAsync();
    }

    [Test, CancelAfter(180000)]
    public async Task ACuredUnitLeavesTheCount()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var ticker = entities.System<GameTicker>();
            var mobs = entities.System<MobStateSystem>();
            var hijack = entities.System<CMUHijackedJoeSystem>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            entities.EnsureComponent<RMCPlanetComponent>(map.MapUid);

            var cured = entities.SpawnEntity(HijackedJoe, map.GridCoords);
            var fighter = entities.SpawnEntity(HijackedJoe, map.GridCoords);
            Assert.That(ticker.StartGameRule(Rule), Is.True);

            hijack.Cure((cured, entities.GetComponent<CMUHijackedJoeComponent>(cured)), "test");
            Assert.That(entities.HasComponent<CMUJoeThreatUnitComponent>(cured), Is.False, "the cure removes the unit");
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound), "one unit is still fighting");

            mobs.ChangeMobState(fighter, MobState.Dead);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(6));
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.PostRound),
                "the repaired Joe, alive on the planet, no longer holds the round open");
        });

        await pair.CleanReturnAsync();
    }
}
