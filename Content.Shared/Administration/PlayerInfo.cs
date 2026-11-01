using Content.Shared.Mind;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility; // CMU14

namespace Content.Shared.Administration;

[Serializable, NetSerializable]
public sealed record PlayerInfo(
    string Username,
    string CharacterName,
    string IdentityName,
    string StartingJob,
    bool Antag,
    ProtoId<RoleTypePrototype>? RoleProto,
    LocId? Subtype,
    int SortWeight,
    NetEntity? NetEntity,
    NetUserId SessionId,
    bool Connected,
    bool ActiveThisRound,
    TimeSpan? OverallPlaytime,
    SpriteSpecifier? AntagIcon = null) // CMU14: icon of the active mind role, shown in admin tooling
{
    private string? _playtimeString;

    public bool IsPinned { get; set; }

    public string PlaytimeString => _playtimeString ??=
        OverallPlaytime?.ToString("%d':'hh':'mm") ?? Loc.GetString("generic-unknown-title");

    public bool Equals(PlayerInfo? other)
    {
        return other?.SessionId == SessionId;
    }

    public override int GetHashCode()
    {
        return SessionId.GetHashCode();
    }
}
