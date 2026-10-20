namespace Content.Shared.CMU14.Ops.ForceOnForce;

/// <summary>
/// Force on Force referee: sudden death at the time limit records who holds the zone. If the
/// side behind on hold-ticks still holds it at the overtime cap they steal the round; otherwise
/// the hold-tick leader wins and an exact tie draws. Announces the score on interval.
/// </summary>
[RegisterComponent]
public sealed partial class FoFTimeLimitRuleComponent : Component
{
    [DataField]
    public TimeSpan TimeLimit = TimeSpan.FromMinutes(75);

    [DataField]
    public TimeSpan ScoreAnnounceInterval = TimeSpan.FromMinutes(10);

    /// <summary>Referee fallback after the limit if nobody holds the zone.</summary>
    [DataField]
    public TimeSpan Overtime = TimeSpan.FromMinutes(15);

    public TimeSpan NextAnnounce;

    public bool SuddenDeathAnnounced;

    /// <summary>Who led the zone at the 75:00 limit (empty if none). Used for veto check.</summary>
    public string ZoneLeaderAtLimit;
}
