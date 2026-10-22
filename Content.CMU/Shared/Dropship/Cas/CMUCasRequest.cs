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
    /// <summary>The uplink created a position or GPS report mark instead of using a flare or laser.</summary>
    public bool RadioMark { get; init; }
    /// <summary>The target is under an RTO's laser designation.</summary>
    public bool Designated { get; init; }
    public CMUCasCarryMode CarryMode { get; init; }
    /// <summary>Seconds left before an undecided accepted request switches to automatic carry-out.</summary>
    public int CarryCountdown { get; init; }
    public int CarryVolleys { get; init; }
    public string StatusDetail { get; init; } = string.Empty;
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
    List<CMUCasGroundReport> gpsReports,
    List<CMUCasRequest> requests,
    int range,
    CMUCasCallTarget ownPosition) : BoundUserInterfaceState
{
    public readonly string Callsign = callsign;
    public readonly List<CMUCasCallTarget> Marks = marks;
    public readonly List<CMUCasGroundReport> GpsReports = gpsReports;
    public readonly List<CMUCasRequest> Requests = requests;
    public readonly int Range = range;
    public readonly CMUCasCallTarget OwnPosition = ownPosition;
}

/// <summary>Sends a request for a mark, ground GPS report, or offset own-position call.</summary>
[Serializable, NetSerializable]
public sealed class CMUCasCallSendMsg(NetEntity? target, CMUCasMunition munition, CMUCasEffect effect, string remarks,
    bool ownPosition = false, int offsetX = 0, int offsetY = 0, int? gpsReportId = null)
    : BoundUserInterfaceMessage
{
    public readonly NetEntity? Target = target;
    public readonly CMUCasMunition Munition = munition;
    public readonly CMUCasEffect Effect = effect;
    public readonly string Remarks = remarks;
    public readonly bool OwnPosition = ownPosition;
    public readonly int OffsetX = offsetX;
    public readonly int OffsetY = offsetY;
    public readonly int? GpsReportId = gpsReportId;
}

[Serializable, NetSerializable]
public sealed class CMUCasRelayPositionMsg : BoundUserInterfaceMessage;

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
