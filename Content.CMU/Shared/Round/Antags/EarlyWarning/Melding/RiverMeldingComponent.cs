namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;

/// <summary>
/// A pending River-Melded conversion (phase E3). Added to the victim when the Early Warning Joe
/// assimilates a console; the melding system warns the victim three times and converts them at
/// <see cref="ConvertAt"/> unless the victim dies, becomes ineligible, or the master is gone.
/// Server only.
/// </summary>
[RegisterComponent]
public sealed partial class RiverMeldingComponent : Component
{
    /// <summary>The Joe this conversion belongs to.</summary>
    [DataField]
    public EntityUid Master;

    [DataField]
    public TimeSpan StartedAt;

    [DataField]
    public TimeSpan ConvertAt;

    /// <summary>How many of the three warnings have gone out.</summary>
    [DataField]
    public int WarningsSent;
}
