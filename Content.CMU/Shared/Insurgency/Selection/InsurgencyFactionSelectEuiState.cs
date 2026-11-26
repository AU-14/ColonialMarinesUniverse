using System.Collections.Generic;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Insurgency.Selection;

/// <summary>Faction ballot options for the current GOVFOR platoon.</summary>
[Serializable, NetSerializable]
public sealed class InsurgencyFactionSelectEuiState : EuiStateBase
{
    public List<DefaultFactionOption> Defaults { get; }

    /// <summary>
    ///     Friendly name of the round's GOVFOR faction (platoon), for the header. May be null before
    ///     a GOVFOR platoon is chosen.
    /// </summary>
    public string? GovforPlatoonName { get; }

    public TimeSpan? Deadline { get; }

    public InsurgencyFactionSelectEuiState(List<DefaultFactionOption> defaults, string? govforPlatoonName, TimeSpan? deadline)
    {
        Deadline = deadline;
        Defaults = defaults;
        GovforPlatoonName = govforPlatoonName;
    }
}

/// <summary>
///     One row in the Default faction list. Carries only what the popup needs to show and to send
///     back a pick; the full definition stays server-side and is applied there by id.
/// </summary>
[Serializable, NetSerializable]
public sealed class DefaultFactionOption
{
    public int Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string Roleplay { get; }
    public string? FlagEntity { get; }
    public string? StatusIcon { get; }

    /// <summary>
    ///     Prototype ids of what this faction's cell kit deploys (placeables, vendor base models, and the
    ///     items its vendors stock), deduplicated and capped. Shown as a sprite grid when the row is
    ///     expanded so the leader can see what they are picking.
    /// </summary>
    public List<string> CellKitEntities { get; }

    /// <summary>Whether this faction opposes the round's chosen GOVFOR platoon. Non-opposing rows are shown greyed.</summary>
    public bool Opposes { get; }

    public DefaultFactionOption(int id, string title, string description, string roleplay, string? flagEntity, string? statusIcon, List<string> cellKitEntities, bool opposes)
    {
        Id = id;
        Title = title;
        Description = description;
        Roleplay = roleplay;
        FlagEntity = flagEntity;
        StatusIcon = statusIcon;
        CellKitEntities = cellKitEntities;
        Opposes = opposes;
    }
}

/// <summary>
///     The leader picked a Default faction by ballot id. The server re-checks the GOVFOR match before
///     applying, so a tampered client cannot force a non-opposing faction.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyFactionSelectDefaultMessage : EuiMessageBase
{
    public int Id { get; }

    public InsurgencyFactionSelectDefaultMessage(int id)
    {
        Id = id;
    }
}

[Serializable, NetSerializable]
public sealed class CMUInsurgencyExclusionsMessage(int[] ids) : EuiMessageBase
{
    public int[] Ids = ids;
}
