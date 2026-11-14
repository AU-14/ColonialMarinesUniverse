using Content.Shared.CMU14.Yautja.BadBlood;

namespace Content.Server.CMU14.Yautja.BadBlood;

public enum BadBloodRoundOutcome : byte
{
    None,

    /// <summary>
    /// Marines win: the Leader is gone and no Lodge stands.
    /// </summary>
    MarinesLeaderAndLodge,

    /// <summary>
    /// Marines win: nobody is left in the clan and nobody came when it called.
    /// </summary>
    MarinesClanWipe,

    /// <summary>
    /// Bad Bloods win: the Lodge stood until the clan ship left.
    /// </summary>
    BadBloodDeparture,
}

/// <summary>
/// What the end rule has decided, and what it has to remember to decide it. Lives on the rule entity next to
/// <see cref="BadBloodCampRuleComponent"/>. The timers themselves are fields of that component.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodCampEndComponent : Component
{
    /// <summary>
    /// Set once, when the rule decides the round. The round itself only ends if one is running.
    /// </summary>
    [ViewVariables]
    public BadBloodRoundOutcome Outcome;

    [ViewVariables]
    public TimeSpan? DecidedAt;

    /// <summary>
    /// Living Bad Bloods nobody is driving, and since when. One still counts as there until it has been without a
    /// player for <see cref="BadBloodCampRuleComponent.LeaderSsdTimeout"/>.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<EntityUid, TimeSpan> AbsentSince = new();

    /// <summary>
    /// When the ship partial last counted the departure timer down.
    /// </summary>
    [ViewVariables]
    public TimeSpan? DepartureCountedAt;
}

/// <summary>
/// The Bad Blood rule has decided the round. Raised once, right before the round is ended.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodRoundDecidedEvent(Entity<BadBloodCampRuleComponent> Rule, BadBloodRoundOutcome Outcome);
