using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._CMU14.Threats;

namespace Content.Server._CMU14.Threats.Rules;

public sealed class InsurgencyEndRuleSystem : GameRuleSystem<InsurgencyRuleComponent>
{
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private const string DefaultWinMsg = "Neutral outcome: The CLF have failed to gain a significant foothold in the colony during their time there. The cell and the military fall into a stalemate, both sides silently operating in the colony for days to come.";

    private static readonly TimeSpan RoundTimeLimit = TimeSpan.FromHours(2);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        if (!_gameTicker.IsGameRuleActive<InsurgencyRuleComponent>())
            return;

        if (_gameTicker.RoundDuration() < RoundTimeLimit)
            return;

        _gameTicker.EndRound(DefaultWinMsg);
    }
}
