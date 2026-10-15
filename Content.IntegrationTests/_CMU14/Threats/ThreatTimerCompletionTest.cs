using Content.Server.CMU14.Threats.Rules;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Threats.Rules;
using Content.Shared.GameTicking;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.CMU14.Threats;

[TestFixture]
public sealed class ThreatTimerCompletionTest
{
    [TestCase("HiveCollapseRule", false)]
    [TestCase("ThreatSurviveRule", false)]
    [TestCase("HiveCollapseRule", true)]
    [TestCase("ThreatSurviveRule", true)]
    public async Task TimerDoesNotEndAnAlreadyFinishedRound(string prototype, bool endedElsewhere)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var ticker = entities.System<GameTicker>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            var rule = ticker.AddGameRule(prototype);
            if (prototype == "HiveCollapseRule")
                entities.GetComponent<HiveCollapseRuleComponent>(rule).HiveCollapseDuration = TimeSpan.Zero;
            else
                typeof(ThreatSurviveRuleComponent).GetProperty(nameof(ThreatSurviveRuleComponent.Minutes))!
                    .SetValue(entities.GetComponent<ThreatSurviveRuleComponent>(rule), 0f);
            Assert.That(ticker.StartGameRule(rule), Is.True);

            if (endedElsewhere)
                ticker.EndRound("Another rule already completed the round.");
            else
                TickRule();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));

            var logger = pair.Server.ResolveDependency<ILogManager>().GetSawmill("ticker");
            var capture = new WarningCapture();
            logger.AddHandler(capture);
            try
            {
                for (var i = 0; i < 30; i++)
                    TickRule();
                Assert.That(capture.Warnings, Is.Empty, "Completed timers must not call EndRound again every tick.");
            }
            finally
            {
                logger.RemoveHandler(capture);
            }

            void TickRule()
            {
                if (prototype == "HiveCollapseRule")
                    entities.System<HiveCollapseRuleSystem>().Update(0);
                else
                    entities.System<ThreatSurviveRuleSystem>().Update(0);
            }
        });
        await pair.CleanReturnAsync();
    }

    private sealed class WarningCapture : ILogHandler
    {
        public readonly List<string> Warnings = new();
        public void Log(string sawmillName, LogEvent message)
        {
            if (message.Level >= LogEventLevel.Warning)
                Warnings.Add(message.RenderMessage());
        }
    }
}
