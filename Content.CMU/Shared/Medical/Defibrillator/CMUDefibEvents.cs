using Robust.Shared.Audio;

namespace Content.Shared.CMU14.Medical.Defibrillator;

/// <summary>
/// Raised on a defibrillator before it starts charging for a shock. Cancelling stops the charge.
/// </summary>
[ByRefEvent]
public record struct CMUDefibZapAttemptEvent(EntityUid User, EntityUid Target, bool Cancelled = false);

/// <summary>
/// Raised on a defibrillator once it has started charging for a shock that lands after <see cref="Delay"/>.
/// </summary>
[ByRefEvent]
public readonly record struct CMUDefibZapStartedEvent(EntityUid User, EntityUid Target, TimeSpan Delay);

/// <summary>
/// Raised on a defibrillator when it plays a voice prompt, so a monitor can hold its alarms until it's finished.
/// </summary>
[ByRefEvent]
public readonly record struct CMUDefibPromptPlayedEvent(SoundSpecifier? Sound);

/// <summary>
/// What a rhythm analysis found: whether the rhythm is shockable, the lowest energy expected to bring the patient
/// back, and whether even that is expected to be enough.
/// </summary>
public readonly record struct CMUDefibAdvice(bool Shockable, int Joules, bool Sufficient);
