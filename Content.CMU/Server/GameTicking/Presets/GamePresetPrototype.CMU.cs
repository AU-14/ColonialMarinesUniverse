namespace Content.Server.GameTicking.Presets;

public sealed partial class GamePresetPrototype
{
    /// <summary>
    /// Controls whether this preset selects no threat, selects one before round start, or runs a post-start vote.
    /// </summary>
    [DataField]
    public CmuThreatSelectionMode ThreatSelectionMode;

    /// <summary>
    /// Whether a selected threat uses its configured spawn-delay range instead of spawning immediately.
    /// </summary>
    [DataField]
    public bool UsesThreatSpawnDelay;

    /// <summary>
    /// Whether this preset uses a GOVFOR platoon even when it does not offer a GOVFOR ballot.
    /// </summary>
    [DataField]
    public bool UsesGovforPlatoon;

    /// <summary>
    /// Whether this preset uses an OPFOR platoon even when it does not offer an OPFOR ballot.
    /// </summary>
    [DataField]
    public bool UsesOpforPlatoon;

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

public enum CmuThreatSelectionMode : byte
{
    Disabled,
    PreRoundstart,
    PostRoundstartVote,
}
