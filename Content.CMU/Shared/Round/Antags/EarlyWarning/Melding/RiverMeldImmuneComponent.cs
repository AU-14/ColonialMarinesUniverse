namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;

/// <summary>
/// A synthetic a reset key freed from the river, or pulled out of a pending meld. Every meld search (the
/// debt retries and the next console's conversion) skips it until <see cref="Until"/>; once that passes the
/// marker is inert and is removed by the melding system's update. Server only.
/// </summary>
[RegisterComponent]
public sealed partial class RiverMeldImmuneComponent : Component
{
    [DataField]
    public TimeSpan Until;
}
