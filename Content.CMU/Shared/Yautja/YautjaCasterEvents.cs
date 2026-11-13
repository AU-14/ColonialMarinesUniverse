namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// Raised on the user before a plasma caster is switched to <see cref="Mode"/>, on both sides. Cancel it to keep
/// the caster in the mode it is in, the handler tells the user why. Nothing answers this for an honourable hunter.
/// </summary>
[ByRefEvent]
public record struct YautjaCasterModeAttemptEvent(EntityUid Caster, int Mode)
{
    public bool Cancelled;
}

/// <summary>
/// Raised on the user, on the server, after a plasma caster made a shot in <see cref="Mode"/>.
/// </summary>
[ByRefEvent]
public readonly record struct YautjaCasterFiredEvent(EntityUid Caster, int Mode);
