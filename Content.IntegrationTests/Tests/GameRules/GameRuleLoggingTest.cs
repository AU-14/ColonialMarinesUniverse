#nullable enable
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Player;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture, TestOf(typeof(GameRuleSystem<>))]
public sealed class GameRuleLoggingTest
{
    [Test]
    public async Task TooFewPlayersLogsRuleAndCounts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        _ = server.System<SandboxRuleSystem>();
        var map = await pair.CreateTestMap();
        var rootLog = server.ResolveDependency<ILogManager>().RootSawmill;
        var logCatcher = new LogCatcher();

        await server.WaitAssertion(() =>
        {
            var rule = entMan.SpawnEntity("Sandbox", map.GridCoords);
            entMan.GetComponent<GameRuleComponent>(rule).MinPlayers = 2;
            var attempt = new RoundStartAttemptEvent(Array.Empty<ICommonSession>(), forced: false);
            rootLog.AddHandler(logCatcher);

            try
            {
                entMan.EventBus.RaiseEvent(EventSource.Local, attempt);

                var messages = logCatcher.CaughtLogs.Select(log => log.RenderMessage()).ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(attempt.Cancelled, Is.True);
                    Assert.That(
                        messages,
                        Does.Contain($"Rule '{entMan.ToPrettyString(rule)}' requires 2 players, but only 0 are ready."));
                });
            }
            finally
            {
                rootLog.RemoveHandler(logCatcher);
            }
        });

        await pair.CleanReturnAsync();
    }
}
