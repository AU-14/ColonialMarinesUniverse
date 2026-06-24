using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Shared.AU14;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Threats.Rules;

public sealed partial class ThreatSurviveRuleSystem : GameRuleSystem<ThreatSurviveRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;

    private TimeSpan? _endTime;
    [Dependency] private GameTicker _gameTicker = default!;
    private float _minutes;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private IGameTiming _timing = default!;

    protected override void Started(EntityUid uid, ThreatSurviveRuleComponent component, GameRuleComponent gameRule,
        GameRuleStartedEvent                  args)
    {
        base.Started(uid, component, gameRule, args);
        _minutes = component.Minutes;
        _endTime = _timing.CurTime + TimeSpan.FromMinutes(_minutes);
    }

    protected override void ActiveTick(EntityUid uid, ThreatSurviveRuleComponent component, GameRuleComponent gameRule,
        float                                    frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        if (_endTime != null && _timing.CurTime >= _endTime)
        {
            string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
            if (!string.IsNullOrEmpty(winMessage))
            {
                _gameTicker.EndRound(winMessage);
                _roundEnd.EndRound();
            }
            else
                _gameTicker.EndRound($"Threat victory: Survived {_minutes} minutes.");

            _roundEnd.EndRound();
        }
    }
}