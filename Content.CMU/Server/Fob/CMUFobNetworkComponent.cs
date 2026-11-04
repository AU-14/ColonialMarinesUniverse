using Content.Shared.CMU14.Fob;

namespace Content.Server.CMU14.Fob;

/// <summary>
/// Round-owned reserve, separate from replaceable consoles and relay fixtures. Never attached to a dropship.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(CMUFobReserveSystem))]
public sealed partial class CMUFobNetworkComponent : Component
{
    public const float DefaultDeploymentRange = 40;

    [DataField] public TimeSpan StartingReserve = TimeSpan.FromMinutes(8);
    [DataField] public TimeSpan Capacity = TimeSpan.FromMinutes(12);
    [DataField] public TimeSpan RelayReward = TimeSpan.FromMinutes(6);
    [DataField] public TimeSpan StartupDuration = TimeSpan.FromSeconds(10);
    [DataField] public TimeSpan MinimumActivation = TimeSpan.FromSeconds(60);
    [DataField] public TimeSpan RestartDelay = TimeSpan.FromSeconds(30);
    [DataField] public TimeSpan RelayInterval = TimeSpan.FromMinutes(5);
    [DataField] public TimeSpan JobLifetime = TimeSpan.FromMinutes(8);
    [DataField] public TimeSpan ExpiredJobDelay = TimeSpan.FromSeconds(30);
    [DataField] public TimeSpan RelayReuseDelay = TimeSpan.FromMinutes(10);
    /// <summary>All deployed support hardware must stay within this radius of the selected LZ.</summary>
    [DataField] public float DeploymentRange = DefaultDeploymentRange;

    [ViewVariables] public string Faction = string.Empty;
    [ViewVariables] public EntityUid Map;
    [ViewVariables] public CMUFobReserveState State;
    [ViewVariables] public TimeSpan StoredReserve;
    [ViewVariables] public int JobGeneration;
    [ViewVariables] public int ActivationGeneration;
    [ViewVariables] public bool BackupUsed;
    [ViewVariables] public EntityUid? FirstRelay;
    [ViewVariables] public EntityUid? SecondRelay;

    [ViewVariables, AutoPausedField] public TimeSpan? DepletesAt;
    [ViewVariables, AutoPausedField] public TimeSpan? StartupEndsAt;
    [ViewVariables, AutoPausedField] public TimeSpan? MinimumActiveUntil;
    [ViewVariables, AutoPausedField] public TimeSpan RestartAvailableAt;
    [ViewVariables, AutoPausedField] public TimeSpan NextJobAt;
    [ViewVariables, AutoPausedField] public TimeSpan? JobExpiresAt;
}
