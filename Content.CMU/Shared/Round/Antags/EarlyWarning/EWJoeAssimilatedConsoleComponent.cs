namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// Marks a console the Early Warning Joe has assimilated. Phase E1 defines the marker so the
/// assimilate objective can read it; phase E2 adds the assimilation action, the lock, the
/// console effects and the repair state machine on top of it.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeAssimilatedConsoleComponent : Component
{
    /// <summary>The Joe that assimilated this console, if known.</summary>
    [DataField]
    public EntityUid? AssimilatedBy;
}
