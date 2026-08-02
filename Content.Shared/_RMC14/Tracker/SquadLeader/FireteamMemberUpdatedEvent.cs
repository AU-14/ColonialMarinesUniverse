namespace Content.Shared._RMC14.Tracker.SquadLeader;

/// <summary>
/// Broadcast after a marine's fireteam assignment changes (assigned, moved or removed).
/// </summary>
[ByRefEvent]
public readonly record struct FireteamMemberUpdatedEvent(EntityUid Member);
