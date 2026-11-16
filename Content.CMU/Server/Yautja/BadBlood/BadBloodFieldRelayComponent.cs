using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-508: a planted field uplink relay. Runs on its own battery, no APC: the charge drains while it is anchored and
/// an empty relay is not a working uplink site. Bad Bloods destroy it outright.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodFieldRelayComponent : Component
{
    /// <summary>
    /// How long the battery lasts while anchored.
    /// </summary>
    [DataField]
    public TimeSpan ChargeDuration = TimeSpan.FromSeconds(3600);

    /// <summary>
    /// Battery time used so far. The relay is empty once this reaches <see cref="ChargeDuration"/>.
    /// </summary>
    [ViewVariables]
    public TimeSpan ChargeUsed;

    [ViewVariables]
    public bool EmptyReported;
}

/// <summary>
/// FLOW-508: the field uplink relay kit from requisitions. Used in hand it plants the relay where the user stands,
/// under open sky.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodFieldRelayKitComponent : Component
{
    [DataField]
    public TimeSpan PlantTime = TimeSpan.FromSeconds(20);

    [DataField]
    public EntProtoId Relay = "CMUBadBloodFieldRelay";
}
