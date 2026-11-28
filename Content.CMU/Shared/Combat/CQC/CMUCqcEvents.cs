using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Combat.CQC;

/// <summary>
/// Raised on the body before a matched sequence fires, after the generic checks (steps, grab, blade, CC window,
/// registered effect). An owner sets <see cref="Cancelled"/> to refuse it (the Early Warning Joe, a <see cref="CMUCqcSequencePrototype.Tier"/>
/// above its level) and <see cref="Empowered"/> to play the effect's empowered branch, when the effect has one.
/// </summary>
[ByRefEvent]
public record struct CMUCqcSequenceAttemptEvent(
    EntityUid Target,
    ProtoId<CMUCqcSequencePrototype> Sequence,
    bool Cancelled = false,
    bool Empowered = false);

/// <summary>Raised on the body after a sequence's effect landed, before the CC window opens.</summary>
[ByRefEvent]
public record struct CMUCqcSequenceFiredEvent(EntityUid Target, ProtoId<CMUCqcSequencePrototype> Sequence, bool Empowered);

/// <summary>
/// Raised on the body after each input was recorded and matched ("match first, gain after": a sequence the input
/// completes has already fired). <see cref="Target"/> is null for an untargeted ability. <see cref="Bodies"/> is how
/// many living bodies a melee swing hit; <see cref="MeleeHit"/> marks an input from a landed melee swing;
/// <see cref="Gain"/> is an explicit override a direct caller asked for.
/// </summary>
[ByRefEvent]
public record struct CMUCqcInputEvent(
    EntityUid? Target,
    CMUCqcInput Input,
    bool FromBlade,
    int Bodies = 1,
    int? Gain = null,
    bool MeleeHit = false);

/// <summary>
/// Raised on the source of a hard CC to scale its <see cref="Duration"/> before the CC window is applied (the Early
/// Warning Joe's xenofauna stun scaling). Raised whether or not the source has a <see cref="CMUCqcComponent"/>.
/// </summary>
[ByRefEvent]
public record struct CMUCqcHardCcDurationEvent(EntityUid Target, TimeSpan Duration);

/// <summary>The two hard CCs the CC window governs. Flashes, dazes, slows and roots are not hard CC.</summary>
public enum CMUHardCcKind : byte
{
    Knockdown,
    Paralyze,
}
