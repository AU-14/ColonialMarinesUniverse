using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;

namespace Content.Server.CMU14.Yautja.BadBlood;

// Broadcast events of the Bad Blood rule and Lodge. Systems outside the two partial classes subscribe to these.
// Partial files of BadBloodCampRuleSystem and BadBloodCampSystem use the partial hooks in the core files instead,
// one class can only subscribe to an event once.

/// <summary>
/// The Bad Blood rule has started and picked its Leader, if one was there to pick.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodRuleStartedEvent(Entity<BadBloodCampRuleComponent> Rule);

/// <summary>
/// The clan's spendable Superiority Points changed.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodSpChangedEvent(Entity<BadBloodCampRuleComponent> Rule, FixedPoint4 Old, FixedPoint4 New);

/// <summary>
/// Leadership moved. Either side can be null: the first Leader has no predecessor, and the rule clears the Leader
/// when it ends.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodLeaderChangedEvent(Entity<BadBloodCampRuleComponent> Rule, EntityUid? Old, EntityUid? New);

/// <summary>
/// The Leader just became gone: dead, deleted, evacuated, or disconnected past the timeout. Raised once per loss.
/// <paramref name="Leader"/> may already be deleted.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodLeaderGoneEvent(Entity<BadBloodCampRuleComponent> Rule, EntityUid? Leader);

/// <summary>
/// A Bad Blood died while the rule runs.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodDiedEvent(Entity<BadBloodCampRuleComponent> Rule, EntityUid Body);

/// <summary>
/// A Lodge now stands. <paramref name="First"/> is true for the clan's first Lodge of the round.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodLodgePitchedEvent(Entity<BadBloodCampRuleComponent> Rule, EntityUid Lodge, bool First);

/// <summary>
/// The Lodge is destroyed or being deleted. It still exists while this is raised, so its containers can be emptied.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodLodgeDestroyedEvent(Entity<BadBloodCampRuleComponent> Rule, EntityUid Lodge);
