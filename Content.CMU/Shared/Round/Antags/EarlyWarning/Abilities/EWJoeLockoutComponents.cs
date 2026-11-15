namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Unwind or Disrupt-II turned this sentry off; the arsenal system re-asserts Off every second until
/// <see cref="Until"/> (no RMC edit needed to gate the sentry's own toggles).
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeSentryLockoutComponent : Component
{
    [DataField]
    public TimeSpan Until;
}

/// <summary>Disrupt-II (A-2) threw this door open; if the pulse bolted it, the bolts drop again at <see cref="Until"/>.</summary>
[RegisterComponent]
public sealed partial class EWJoeDoorHeldOpenComponent : Component
{
    [DataField]
    public TimeSpan Until;

    /// <summary>True when the pulse itself dropped the bolts; only then does expiry lift them.</summary>
    [DataField]
    public bool Bolted;
}
