namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;

/// <summary>
/// RB-13: raised on the Joe before a Test of Faith or Judgement flash stuns <see cref="Target"/>, so the server's
/// CC-window rule can shorten it (xenofauna scaling, then half inside the target's window). Unhandled (no Liturgy
/// meter, or the client), the flash uses the xenofauna-scaled base duration.
/// </summary>
[ByRefEvent]
public record struct EWJoeFlashStunEvent(EntityUid Target, TimeSpan Duration, bool Handled = false);

/// <summary>RB-13: raised on the Joe after its flash stunned <see cref="Target"/>: the flash opens the target's CC window.</summary>
[ByRefEvent]
public readonly record struct EWJoeFlashStunLandedEvent(EntityUid Target);
