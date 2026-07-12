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

/// <summary>
///     Client asks the server to build a ready-to-fill faction spreadsheet (.xlsx bytes). The server
///     bakes every valid entity/icon/job/flag/platoon (by localized name) into the workbook's dropdowns
///     and embeds the field help, then replies with <see cref="InsurgencyExportSheetResultMessage"/>.
///     When <see cref="FactionId"/> is set, the sheet is pre-filled with that stored faction's data so a
///     host can round-trip an existing faction; when null it is a blank template to hand to a player.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyExportSheetMessage : EuiMessageBase
{
    public int? FactionId { get; }

    public InsurgencyExportSheetMessage(int? factionId)
    {
        FactionId = factionId;
    }
}

/// <summary>
///     Server's reply carrying the generated workbook as raw .xlsx bytes plus a suggested file name.
///     The client writes it to disk via a save dialog. No macros, so the player who receives it just
///     fills the dropdowns and sends it back - nothing to enable or install.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyExportSheetResultMessage : EuiMessageBase
{
    public byte[] Workbook { get; }
    public string FileName { get; }

    public InsurgencyExportSheetResultMessage(byte[] workbook, string fileName)
    {
        Workbook = workbook;
        FileName = fileName;
    }
}

/// <summary>
///     Client uploads a filled-in faction spreadsheet (.xlsx bytes) to be imported. The server reads the
///     known sheets into a <see cref="FactionDefinition"/> and runs it through the same validator every
///     untrusted payload passes (caps + unknown-prototype stripping), so nothing in the file is trusted
///     as-is. Only the cell values are ever read; no formulas or macros are executed.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyImportSheetMessage : EuiMessageBase
{
    public byte[] Workbook { get; }

    public InsurgencyImportSheetMessage(byte[] workbook)
    {
        Workbook = workbook;
    }
}
