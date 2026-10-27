using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.CrewManifest;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Lobby;

[Serializable, NetSerializable]
public sealed class CmuLobbyRosterRequest(NetEntity station) : EntityEventArgs
{
    public NetEntity Station { get; } = station;
}

/// <summary>Only public manifest fields and the arrival portrait; never the private character records.</summary>
[Serializable, NetSerializable]
public sealed class CmuLobbyRosterEntry(CrewManifestEntry record, CMURecordPortrait? portrait,
    Dictionary<string, EntProtoId>? outfit = null, bool identityHidden = false)
{
    public CrewManifestEntry Record { get; } = record;
    public CMURecordPortrait? Portrait { get; } = portrait;
    // Visible issued clothing only; no pocket contents, current equipment, or private loadout preferences.
    public Dictionary<string, EntProtoId>? Outfit { get; } = outfit;
    // Anonymous occupied slots carry no character name, squad, portrait or outfit.
    public bool IdentityHidden { get; } = identityHidden;
}

[Serializable, NetSerializable]
public sealed class CmuLobbyRosterResponse(NetEntity station, List<CmuLobbyRosterEntry> entries) : EntityEventArgs
{
    public NetEntity Station { get; } = station;
    public List<CmuLobbyRosterEntry> Entries { get; } = entries;
}
