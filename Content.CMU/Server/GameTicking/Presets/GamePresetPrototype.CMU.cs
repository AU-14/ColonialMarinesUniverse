namespace Content.Server.GameTicking.Presets;

public sealed partial class GamePresetPrototype
{
    /// <summary>
    /// Whether this preset starts the automatic third-party queue without a selected threat.
    /// </summary>
    [DataField]
    public bool ThirdPartyAutoSpawn;

    /// <summary>
    /// Seconds between preset-owned automatic third-party spawn attempts.
    /// </summary>
    [DataField]
    public int ThirdPartyInterval = 14000;

    /// <summary>
    /// Maximum fraction of the current population used to preselect third-party bodies.
    /// </summary>
    [DataField]
    public float ThirdPartyRatio = 0.15f;

    /// <summary>
    /// Maximum number of third parties selected for the preset-owned queue.
    /// </summary>
    [DataField]
    public int MaxThirdParties = 7;
}
