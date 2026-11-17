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
/// A Bad Blood's personal Honour changed. <paramref name="Holder"/> is the mind (or body) the ledger keys on.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodHonourChangedEvent(Entity<BadBloodCampRuleComponent> Rule, EntityUid Holder);

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

// ---- objectives (docs/GraphAudit-BadBlood-ObjectivesPlan.md ARCH-501). Every event below is broadcast. ----

/// <summary>
/// Why the Lodge was put on the marine tactical map.
/// </summary>
public enum BadBloodRevealSource : byte
{
    Other,
    Attack,
    TotemFell,
    Squad,
    ShipPhase,
    Intel,
}

/// <summary>
/// A stage of the marine objective chain, for the round-end objective timeline.
/// </summary>
public enum BadBloodObjectiveStage : byte
{
    GatePart,
    GateMet,
    Signature,
    Uplink,
    StrikeStarted,
    StrikePaused,
    StrikeResumed,
    Rerouted,
    StrikeCancelled,
    StrikeCompleted,
    Hijack,
}

/// <summary>
/// Marines finished desecrating a Totem: it pays nothing, is not ripe and has no aura until it is reconsecrated.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodTotemDesecratedEvent(EntityUid Totem);

/// <summary>
/// Bad Bloods finished reconsecrating a desecrated Totem.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodTotemReconsecratedEvent(EntityUid Totem);

/// <summary>
/// A marine read a desecrated Totem for intel.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodTotemReadEvent(EntityUid Totem, EntityUid Reader);

/// <summary>
/// A Totem's paid income crossed <paramref name="WholePoints"/> more whole Superiority Points.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodTotemPaidEvent(EntityUid Totem, int WholePoints);

/// <summary>
/// A trophy offering was credited at the Lodge.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodOfferingCreditedEvent(EntityUid Lodge, EntityUid Trophy, EntityUid User);

/// <summary>
/// The Lodge was revealed on the marine tactical map. A null <paramref name="Duration"/> reveals it until cleared.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodLodgeRevealedEvent(EntityUid Lodge, TimeSpan? Duration, BadBloodRevealSource Source);

/// <summary>
/// Enough ripe Totems are held for the ship call to start counting. The ship is called at <paramref name="CallAt"/>.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodRipeHoldStartedEvent(Entity<BadBloodCampRuleComponent> Rule, TimeSpan CallAt);

/// <summary>
/// The ripe-Totem hold was broken before the ship was called.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodRipeHoldBrokenEvent(Entity<BadBloodCampRuleComponent> Rule);

/// <summary>
/// The clan ship was called and arrives at <paramref name="ArrivesAt"/>.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodShipCalledEvent(Entity<BadBloodCampRuleComponent> Rule, TimeSpan ArrivesAt);

/// <summary>
/// A marine decrypted a Bad Blood bracer's data-core.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodBracerDecryptedEvent(EntityUid Bracer, EntityUid User);

/// <summary>
/// A non-Bad Blood first picked up the clan star chart from the Lodge.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodStarChartRecoveredEvent(EntityUid Chart, EntityUid User);

/// <summary>
/// A non-Bad Blood stripped a trophy from the Lodge rack.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodTrophyStrippedEvent(EntityUid Lodge, EntityUid Trophy, EntityUid User);

/// <summary>
/// A Bad Blood sabotaged a marine objective site (an uplink site or a colony reactor).
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodSiteSabotagedEvent(EntityUid Site, EntityUid User);

/// <summary>
/// The marine objective chain or the Vessel Strike moved to <paramref name="Stage"/>.
/// <paramref name="Site"/> is the uplink site, reactor or console involved, when there is one.
/// </summary>
[ByRefEvent]
public readonly record struct BadBloodObjectiveStageEvent(BadBloodObjectiveStage Stage, string Detail, EntityUid? Site = null);
