using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.SporeSac;

/// <summary>
/// Structure that bursts and releases a spore cloud when a valid host walks
/// near it, or when its health reaches zero. Can regenerate and burst
/// multiple times up to MaxBatches.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUPathogenSporeSacSystem), typeof(CMUXenoSporeSacSystem))]
public sealed partial class CMUPathogenSporeSacComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 Health = 80;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxHealth = 80;

    [DataField, AutoNetworkedField]
    public SporeSacStatus Status = SporeSacStatus.Waiting;

    /// <summary>How many spore clouds this sac has produced so far.</summary>
    [DataField, AutoNetworkedField]
    public int SporeBatch = 1;

    /// <summary>0 = unlimited batches.</summary>
    [DataField, AutoNetworkedField]
    public int MaxBatches;

    [DataField, AutoNetworkedField]
    public bool SilentRelease;

    [DataField, AutoNetworkedField]
    public TimeSpan? RegenerateAt;

    [DataField, AutoNetworkedField]
    public TimeSpan BurstToReleaseDelay = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan RegenerationTime = TimeSpan.FromMinutes(2);

    [DataField, AutoNetworkedField]
    public TimeSpan? BurstAt;

    /// <summary>The xeno that placed this sac (for cleanup + hive lookup).</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Placer;

    [DataField, AutoNetworkedField]
    public EntProtoId CloudPrototype = "CMUPathogenSporeCloud";
}

public enum SporeSacStatus : byte
{
    Waiting,
    Deploying,
    Deployed,
}