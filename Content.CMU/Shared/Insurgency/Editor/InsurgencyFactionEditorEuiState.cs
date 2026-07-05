using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Insurgency.Editor;

/// <summary>
///     State pushed to the faction editor client. Carries every stored faction as a full
///     <see cref="FactionDefinition"/> (already NetSerializable) plus the round's current GOVFOR
///     platoon id so the editor can hint which factions currently match.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyFactionEditorEuiState : EuiStateBase
{
    public List<EditorFactionEntry> Factions { get; }

    /// <summary>
    ///     Platoon id of the round's GOVFOR faction (USMC, TWE RMC, UPP, and so on), or null if not
    ///     selected yet. Used only as an in-editor hint; the server is authoritative on matching
    ///     when a faction is applied.
    /// </summary>
    public string? GovforPlatoon { get; }

    /// <summary>
    ///     Which editor the server opened. The client only adapts its UI to this; the server enforces
    ///     the scope on every message regardless of what the client shows.
    /// </summary>
    public InsurgencyEditorScope Scope { get; }

    public InsurgencyFactionEditorEuiState(List<EditorFactionEntry> factions, string? govforPlatoon, InsurgencyEditorScope scope)
    {
        Factions = factions;
        GovforPlatoon = govforPlatoon;
        Scope = scope;
    }
}

/// <summary>
///     The two faction-editor entry points. Default is the host-flag INSFOR editor over host-authored
///     factions; Custom is the separate editor (own console command, own authorization gate) over
///     player-authored Custom factions only.
/// </summary>
[Serializable, NetSerializable]
public enum InsurgencyEditorScope : byte
{
    Default,
    Custom,
}

[Serializable, NetSerializable]
public sealed class EditorFactionEntry
{
    public int Id { get; }
    public bool IsDefault { get; }
    public FactionDefinition Definition { get; }

    public EditorFactionEntry(int id, bool isDefault, FactionDefinition definition)
    {
        Id = id;
        IsDefault = isDefault;
        Definition = definition;
    }
}

/// <summary>
///     Create (Id null) or update (Id set) a faction. The server revalidates and clamps the
///     definition before storing; client-sent values are never trusted as-is.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyFactionSaveMessage : EuiMessageBase
{
    public int? Id { get; }
    public bool IsDefault { get; }
    public FactionDefinition Definition { get; }

    public InsurgencyFactionSaveMessage(int? id, bool isDefault, FactionDefinition definition)
    {
        Id = id;
        IsDefault = isDefault;
        Definition = definition;
    }
}

[Serializable, NetSerializable]
public sealed class InsurgencyFactionDeleteMessage : EuiMessageBase
{
    public int Id { get; }

    public InsurgencyFactionDeleteMessage(int id)
    {
        Id = id;
    }
}

/// <summary>
///     Apply a stored faction for the current round. Server revalidates GOVFOR matching.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyFactionSelectMessage : EuiMessageBase
{
    public int Id { get; }

    public InsurgencyFactionSelectMessage(int id)
    {
        Id = id;
    }
}

[Serializable, NetSerializable]
public sealed class InsurgencyFactionRefreshMessage : EuiMessageBase
{
}
