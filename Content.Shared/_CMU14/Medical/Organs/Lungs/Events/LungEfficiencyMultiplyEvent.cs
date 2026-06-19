namespace Content.Shared._CMU14.Medical.Organs.Lungs.Events;

/// <summary>
///     Raised by-ref on a respirator-bearing entity once per inhale cycle so the
///     CMU lung subsystem can scale the entity's <c>BreathVolume</c>. Default
///     <see cref="Multiplier"/> of <c>1.0</c> leaves upstream behaviour unchanged
///     when no CMU lungs subscribe.
/// </summary>
[ByRefEvent]
public record struct LungEfficiencyMultiplyEvent(EntityUid Body, float Multiplier);
