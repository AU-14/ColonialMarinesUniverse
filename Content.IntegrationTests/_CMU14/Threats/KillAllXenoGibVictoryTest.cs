using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Gibbing;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Threats;

[TestFixture]
public sealed class KillAllXenoGibVictoryTest
{
    private static readonly Robust.Shared.Prototypes.EntProtoId KillAllXenoRulePrototype = "KillAllXenoRule";

    // the last xeno alive (a ship-side larva in the report) got admin gibbed, it never went Dead so
    // the death hook never re-checked and the round sat there forever
    [Test, CancelAfter(180000)]
    public async Task GibbingTheLastXenoEndsTheRound()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ticker = entities.System<GameTicker>();
            var mobs = entities.System<MobStateSystem>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            var drone = entities.SpawnEntity("CMXenoDrone", map.GridCoords);
            var larva = entities.SpawnEntity("CMXenoLarva", map.GridCoords);
            Assert.That(ticker.StartGameRule(KillAllXenoRulePrototype), Is.True);

            mobs.ChangeMobState(drone, MobState.Dead);
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound),
                "a living larva is still a xeno, the round keeps going");

            entities.System<GibbingSystem>().Gib(larva);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(6));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.PostRound),
                "with the last xeno gibbed the threat is gone and the round has to end");
        });
        await pair.CleanReturnAsync();
    }
}
