using Content.Server.CMU14.Yautja;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Yautja;
using Content.Shared.GameTicking;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaHuntRollTest
{
    [TestCase("Insurgency", 1f / 6f)]
    [TestCase("insurgency", 1f / 6f)]
    [TestCase("DistressSignal", 1f / 3f)]
    [TestCase("ColonyFall", 0f)]
    [TestCase("ForceOnForce", 0f)]
    [TestCase(null, 0f)]
    public void OnlyInsurgencyAndDistressSignalRoll(string? preset, float expected)
    {
        Assert.That(YautjaHuntRoll.ChanceFor(preset, 1f / 6f, 1f / 3f), Is.EqualTo(expected));
    }

    [Test]
    public void ChancesAreClamped()
    {
        Assert.Multiple(() =>
        {
            Assert.That(YautjaHuntRoll.ChanceFor("Insurgency", 2f, 0f), Is.EqualTo(1f));
            Assert.That(YautjaHuntRoll.ChanceFor("DistressSignal", 0f, -1f), Is.EqualTo(0f));
        });
    }

    [Test]
    public async Task LobbyRollFollowsThePresetAndLeavesStaffHuntsAlone()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            InLobby = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        var round = server.System<YautjaPredatorRoundSystem>();
        var cfg = server.CfgMan;

        try
        {
            await server.WaitPost(() =>
            {
                cfg.SetCVar(YautjaPredatorRoundCVars.InsurgencyChance, 1f);
                cfg.SetCVar(YautjaPredatorRoundCVars.DistressSignalChance, 0f);
                ticker.SetGamePreset("Insurgency");
                cfg.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, true);
            });
            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
                Assert.That(round.HuntActive, Is.True, "Insurgency at 100% should have queued a hunt");
            });

            // gamemode changed after the roll, the rolled hunt goes away
            await server.WaitPost(() => ticker.SetGamePreset("ColonyFall"));
            await pair.RunTicksSync(5);
            await server.WaitAssertion(() => Assert.That(round.HuntActive, Is.False));

            await server.WaitPost(() => ticker.SetGamePreset("DistressSignal"));
            await pair.RunTicksSync(5);
            await server.WaitAssertion(() => Assert.That(round.HuntActive, Is.False, "DS at 0% never rolls"));

            // staff can still force one on any mode, and a preset change doesn't take it back
            await server.WaitPost(() =>
            {
                ticker.SetGamePreset("ColonyFall");
                Assert.That(round.TryInitializePredatorRound(out _), Is.True);
            });
            await pair.RunTicksSync(5);
            await server.WaitPost(() => ticker.SetGamePreset("ForceOnForce"));
            await pair.RunTicksSync(5);
            await server.WaitAssertion(() => Assert.That(round.HuntActive, Is.True));
        }
        finally
        {
            await server.WaitPost(() =>
            {
                cfg.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, false);
                cfg.SetCVar(YautjaPredatorRoundCVars.InsurgencyChance, YautjaPredatorRoundCVars.InsurgencyChance.DefaultValue);
                cfg.SetCVar(YautjaPredatorRoundCVars.DistressSignalChance, YautjaPredatorRoundCVars.DistressSignalChance.DefaultValue);
            });
        }

        await pair.CleanReturnAsync();
    }
}
