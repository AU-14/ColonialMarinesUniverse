using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Dropship.Cas;

[Serializable, NetSerializable]
public enum CMUCasRequestStatus : byte
{
    Pending,
    Accepted,
    Denied,
    ClearedHot,
    Complete,
}

[Serializable, NetSerializable]
public enum CMUCasMunition : byte
{
    Any,
    Guns,
    Rockets,
    Bombs,
    Lasers,
    Gau,
    Bofors,
    Howitzer,
    Missiles,
    ChinGun,
}

/// <summary>The effect the RTO wants on target, passed to the aircrew with the request.</summary>
[Serializable, NetSerializable]
public enum CMUCasEffect : byte
{
    Any,
    HighExplosive,
    ArmorPiercing,
    Incendiary,
    Fragmentation,
    Smoke,
    Illumination,
}

/// <summary>How an accepted request is prosecuted. Undecided turns into Auto when the decision window lapses.</summary>
[Serializable, NetSerializable]
public enum CMUCasCarryMode : byte
{
    Undecided,
    Auto,
    Manual,
}

/// <summary>
/// An RTO's 9-line style close air support request, mirrored to every weapons console of the requesting faction.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct CMUCasRequest(
    int Id,
    string Callsign,
    NetEntity Target,
    string TargetName,
    string Location,
    string TargetDescription,
    string Friendlies,
    CMUCasMunition Munition,
    bool DangerClose,
    CMUCasRequestStatus Status,
    string? Aircraft)
{
    public CMUCasEffect Effect { get; init; }
    public string Remarks { get; init; } = string.Empty;
    /// <summary>The RTO had no flare out; the uplink dropped a mark on the nearest hostile.</summary>
    public bool RadioMark { get; init; }
    /// <summary>The target is under an RTO's laser designation.</summary>
    public bool Designated { get; init; }
    public CMUCasCarryMode CarryMode { get; init; }
    /// <summary>Seconds left before an undecided accepted request switches to automatic carry-out.</summary>
    public int CarryCountdown { get; init; }
    public int CarryVolleys { get; init; }
}

[Serializable, NetSerializable]
public sealed class CMUCasRequestActionMsg(int id, CMUCasRequestStatus status) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
    public readonly CMUCasRequestStatus Status = status;
}

[Serializable, NetSerializable]
public sealed class CMUCasCarryModeMsg(int id, CMUCasCarryMode mode) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
    public readonly CMUCasCarryMode Mode = mode;
}

[Serializable, NetSerializable]
public enum CMUCasCallUi : byte
{
    Key,
}

/// <summary>A mark the RTO can call CAS onto, with the 9-line data the uplink would send for it.</summary>
[Serializable, NetSerializable]
public sealed record CMUCasCallTarget(
    NetEntity? Target,
    string Name,
    string Location,
    string Description,
    string Friendlies,
    int Distance,
    bool DangerClose,
    bool Designated);

[Serializable, NetSerializable]
public sealed class CMUCasCallBuiState(
    string callsign,
    List<CMUCasCallTarget> marks,
    CMUCasCallTarget? nearestHostile,
    List<CMUCasRequest> requests,
    int range) : BoundUserInterfaceState
{
    public readonly string Callsign = callsign;
    public readonly List<CMUCasCallTarget> Marks = marks;
    /// <summary>The nearest hostile with no mark on it, for a call without a flare.</summary>
    public readonly CMUCasCallTarget? NearestHostile = nearestHostile;
    public readonly List<CMUCasRequest> Requests = requests;
    public readonly int Range = range;
}

/// <summary>Sends a request. A null target drops a radio mark on the nearest hostile.</summary>
[Serializable, NetSerializable]
public sealed class CMUCasCallSendMsg(NetEntity? target, CMUCasMunition munition, CMUCasEffect effect, string remarks)
    : BoundUserInterfaceMessage
{
    public readonly NetEntity? Target = target;
    public readonly CMUCasMunition Munition = munition;
    public readonly CMUCasEffect Effect = effect;
    public readonly string Remarks = remarks;
}

[Serializable, NetSerializable]
public sealed class CMUCasCallClearHotMsg(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed class CMUCasCallAbortMsg(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

/// <summary>Raised on the server so the faction's JTAC radio relays CAS request traffic.</summary>
[ByRefEvent]
public readonly record struct CMUCasRadioEvent(
    EntityUid Source,
    string Faction,
    string Message);
