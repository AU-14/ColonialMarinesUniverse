namespace Content.Shared._CMU14.Round.Antags.Vigilante;

/// <summary>
/// A vigilante antag. Shortly after spawn they are briefed with the names of the
/// colony's organized crime members, whom they consider fair game.
/// </summary>
[RegisterComponent]
public sealed partial class VigilanteComponent : Component
{
    /// <summary>
    /// Delay before the target list is sent, so the mob has time to spawn.
    /// </summary>
    [DataField]
    public TimeSpan FaxDelay = TimeSpan.FromSeconds(180);

    /// <summary>
    /// Mob members on the vigilante's briefing; reported at round end.
    /// </summary>
    public int TargetCount;

    public TimeSpan NextFax;
    public bool Faxed;
}
