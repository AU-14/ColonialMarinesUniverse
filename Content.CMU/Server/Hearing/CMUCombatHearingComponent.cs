namespace Content.Server.CMU14.Hearing;

/// <summary>
///     Slowly builds up hearing damage from nearby gunfire and explosions when the wearer has no ear protection.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUCombatHearingSystem))]
public sealed partial class CMUCombatHearingComponent : Component
{
    [DataField]
    public float Exposure;

    [DataField]
    public float MaxExposure = 600f;

    /// <summary>
    ///     Exposure lost per second while it is quiet.
    /// </summary>
    [DataField]
    public float DecayPerSecond = 0.05f;

    [DataField]
    public float GunRange = 6f;

    [DataField]
    public float GunExposure = 0.5f;

    [DataField]
    public float ShooterMultiplier = 3f;

    [DataField]
    public float SuppressedMultiplier = 0.25f;

    [DataField]
    public float ExplosionExposure = 20f;

    [DataField]
    public float ExplosionRangeBase = 4f;

    [DataField]
    public float ExplosionRangePerTile = 2f;

    [DataField]
    public float ExplosionRangeMax = 20f;

    /// <summary>
    ///     Exposure needed for each warning tier. Tier 2 and up can deafen on explosions,
    ///     tier 3 also causes deafness episodes on its own.
    /// </summary>
    [DataField]
    public List<float> Thresholds = new() { 150f, 300f, 450f };

    [DataField]
    public List<string> WarningMessages = new()
    {
        "cmu-combat-hearing-tier-1",
        "cmu-combat-hearing-tier-2",
        "cmu-combat-hearing-tier-3",
    };

    [DataField]
    public string RecoveredMessage = "cmu-combat-hearing-recovered";

    [DataField]
    public int Tier;

    [DataField]
    public TimeSpan ExplosionDeafTier2 = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan ExplosionDeafTier3 = TimeSpan.FromSeconds(12);

    [DataField]
    public float GunDeafChanceTier3 = 0.03f;

    [DataField]
    public TimeSpan GunDeafTier3 = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan EpisodeIntervalMin = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan EpisodeIntervalMax = TimeSpan.FromSeconds(120);

    [DataField]
    public TimeSpan EpisodeDurationMin = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan EpisodeDurationMax = TimeSpan.FromSeconds(10);

    [DataField, AutoPausedField]
    public TimeSpan NextEpisodeAt;
}
