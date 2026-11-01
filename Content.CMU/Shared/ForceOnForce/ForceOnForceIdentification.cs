namespace Content.Shared.CMU14.ForceOnForce;

/// <summary>How the Force on Force identification HUD treats a living target.</summary>
public enum ForceOnForceIdentification
{
    /// <summary>Friendly or identified: keep normal identifiers.</summary>
    None,

    /// <summary>The direct competition: exclamation mark.</summary>
    Hostile,

    /// <summary>Unfamiliar party: question mark.</summary>
    Unknown,
}
