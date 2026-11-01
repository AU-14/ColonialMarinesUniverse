namespace Content.Shared.CMU14.Squads;

/// <summary>
/// Raised to check whether someone can be automatically made a squad or fireteam leader.
/// Lets server-only checks, like being AFK, rule them out.
/// </summary>
[ByRefEvent]
public record struct CMUCanAutoLeadEvent(EntityUid Uid, bool Cancelled = false);
