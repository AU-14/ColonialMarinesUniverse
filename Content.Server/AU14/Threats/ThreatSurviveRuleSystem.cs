using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.AU14.Threats;
using Content.Shared.GameTicking.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Threats;

public sealed class ThreatSurviveRuleSystem : GameRuleSystem<ThreatSurviveRuleComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly Content.Server.AU14.Round.AuRoundSystem _auRoundSystem = default!;

    private TimeSpan? _endTime;
    private float _minutes = 0f;

    protected override void Started(EntityUid uid, ThreatSurviveRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        _minutes = component.Minutes;
        _endTime = _timing.CurTime + TimeSpan.FromMinutes(_minutes);
    }

    protected override void ActiveTick(EntityUid uid, ThreatSurviveRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        if (_endTime != null && _timing.CurTime >= _endTime)
        {
            var winMessage = _auRoundSystem._selectedthreat?.WinMessage;
            if (!string.IsNullOrEmpty(winMessage))
                _gameTicker.EndRound(winMessage);
            else
                _gameTicker.EndRound($"Threat victory: Survived {_minutes} minutes.");
            _endTime = null;
        }
    }
}
