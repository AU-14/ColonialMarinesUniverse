namespace Content.Shared.AU14;

/// <summary>
/// Win condition rule that fires when a percentage of abominations are dead.
/// Counts both natural-form abominations (AbominationComponent) and
/// currently-disguised mimics (AbominationMimicTransformedComponent) — the
/// disguise wears the flesh underneath, so it counts as the threat for win
/// purposes.
/// </summary>
[RegisterComponent]
public sealed partial class KillAllAbominationsRuleComponent : Component
{
    /// <summary>
    /// Percentage of abominations that must be dead to trigger victory (0-100).
    /// Default 100 preserves "all dead" behaviour.
    /// </summary>
    [DataField("percent")]
    public int Percent = 100;
}
