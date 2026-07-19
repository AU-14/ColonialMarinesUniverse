#nullable enable
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture, TestOf(typeof(GameTicker))]
public sealed class GamePresetFallbackResetTest
{
    private const string DefaultPreset = "TestFallbackResetDefaultPreset";
    private const string FailingPreset = "TestFallbackResetFailingPreset";
    private const string FallbackPreset = "TestFallbackResetViablePreset";

    [TestPrototypes]
    private const string Prototypes = """
        - type: gamePreset
          id: TestFallbackResetDefaultPreset
          name: fallback reset default
          description: Test default preset after fallback.
          showInVote: false
          rules: []

        - type: gamePreset
          id: TestFallbackResetFailingPreset
          name: fallback reset failing
          description: Test preset whose first start attempt is cancelled.
          showInVote: false
          rules: []

        - type: gamePreset
          id: TestFallbackResetViablePreset
          name: fallback reset viable
          description: Test fallback preset that succeeds on its first attempt.
          showInVote: false
          rules: []
        """;

    [Test]
    public async Task SuccessfulFallback_AfterOneRound_ResetsToConfiguredDefault()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = pair.Server;
        var cfg = server.CfgMan;
        var ticker = server.System<GameTicker>();
        var canceller = server.System<CancelNextFallbackPresetStartSystem>();
        var oldGridFill = cfg.GetCVar(CCVars.GridFill);
        var oldFallbackEnabled = cfg.GetCVar(CCVars.GameLobbyFallbackEnabled);
        var oldFallbackPreset = cfg.GetCVar(CCVars.GameLobbyFallbackPreset);
        var oldDefaultPreset = cfg.GetCVar(CCVars.GameLobbyDefaultPreset);

        try
        {
            cfg.SetCVar(CCVars.GridFill, true);
            cfg.SetCVar(CCVars.GameLobbyFallbackEnabled, true);
            cfg.SetCVar(CCVars.GameLobbyFallbackPreset, FallbackPreset);
            cfg.SetCVar(CCVars.GameLobbyDefaultPreset, DefaultPreset);
            ticker.SetGamePreset(FailingPreset);
            canceller.CancelNextAttempt = true;

            await pair.WaitCommand("startround");
            await pair.RunTicksSync(10);

            Assert.Multiple(() =>
            {
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
                Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo(FallbackPreset));
                Assert.That(ticker.Preset?.ID, Is.EqualTo(FallbackPreset));
                Assert.That(ticker.ResetCountdown, Is.Zero);
            });

            await server.WaitPost(() => ticker.RestartRound());
            await pair.RunTicksSync(1);

            Assert.Multiple(() =>
            {
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
                Assert.That(ticker.CurrentPreset, Is.Null);
                Assert.That(ticker.Preset?.ID, Is.EqualTo(DefaultPreset));
                Assert.That(ticker.ResetCountdown, Is.Null);
            });
        }
        finally
        {
            canceller.CancelNextAttempt = false;
            ticker.SetGamePreset((GamePresetPrototype?) null);
            cfg.SetCVar(CCVars.GridFill, oldGridFill);
            cfg.SetCVar(CCVars.GameLobbyFallbackEnabled, oldFallbackEnabled);
            cfg.SetCVar(CCVars.GameLobbyFallbackPreset, oldFallbackPreset);
            cfg.SetCVar(CCVars.GameLobbyDefaultPreset, oldDefaultPreset);
            await pair.CleanReturnAsync();
        }
    }
}

public sealed class CancelNextFallbackPresetStartSystem : EntitySystem
{
    public bool CancelNextAttempt;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
    }

    private void OnStartAttempt(RoundStartAttemptEvent args)
    {
        if (!CancelNextAttempt || args.Forced)
            return;

        CancelNextAttempt = false;
        args.Cancel();
    }
}
