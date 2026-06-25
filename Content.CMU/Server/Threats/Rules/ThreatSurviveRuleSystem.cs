using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Timing;
using ThreatSurviveRuleComponent = Content.Shared._CMU14.Threats.Rules.ThreatSurviveRuleComponent;

namespace Content.Server._CMU14.Threats.Rules;

public sealed partial class ThreatSurviveRuleSystem : GameRuleSystem<ThreatSurviveRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan? _endTime;
    private float _minutes;

    protected override void Started(EntityUid uid, ThreatSurviveRuleComponent component, GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        _minutes = component.Minutes;
        _endTime = _timing.CurTime + TimeSpan.FromMinutes(_minutes);
    }

    protected override void ActiveTick(EntityUid uid, ThreatSurviveRuleComponent component, GameRuleComponent gameRule,
        float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (_endTime == null || !(_timing.CurTime >= _endTime))
            return;

        string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
        _gameTicker.EndRound(!string.IsNullOrEmpty(winMessage)
            ? winMessage
            : $"Threat victory: Survived {_minutes} minutes.");
        _roundEnd.EndRound();
    }
}
