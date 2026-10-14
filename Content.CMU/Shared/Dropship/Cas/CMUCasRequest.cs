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
    string? Aircraft);

[Serializable, NetSerializable]
public sealed class CMUCasRequestActionMsg(int id, CMUCasRequestStatus status) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
    public readonly CMUCasRequestStatus Status = status;
}

/// <summary>Raised on the server so the faction's JTAC radio relays CAS request traffic.</summary>
[ByRefEvent]
public readonly record struct CMUCasRadioEvent(
    EntityUid Source,
    string Faction,
    string Message);
