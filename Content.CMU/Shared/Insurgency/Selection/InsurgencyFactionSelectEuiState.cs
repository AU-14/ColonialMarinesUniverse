using System.Collections.Generic;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Insurgency.Selection;

/// <summary>
///     State for the CLF-leader faction selection popup. Carries the round's Default factions (List A)
///     with a per-faction flag for whether it opposes the round's GOVFOR platoon, plus whether this
///     player is authorized to pick a Custom faction (List B). The Custom list itself is read from the
///     player's own local files client-side, so it never travels over the wire here.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyFactionSelectEuiState : EuiStateBase
{
    public List<DefaultFactionOption> Defaults { get; }

    /// <summary>
    ///     True if this player holds the authorization flag for Custom factions. The server re-checks
    ///     this on every custom pick; the client only uses it to enable or grey out List B.
    /// </summary>
    public bool CanUseCustom { get; }

    /// <summary>
    ///     Friendly name of the round's GOVFOR faction (platoon), for the header. May be null before
    ///     a GOVFOR platoon is chosen.
    /// </summary>
    public string? GovforPlatoonName { get; }

    public InsurgencyFactionSelectEuiState(List<DefaultFactionOption> defaults, bool canUseCustom, string? govforPlatoonName)
    {
        Defaults = defaults;
        CanUseCustom = canUseCustom;
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
///     The leader picked a Default faction by DB id. The server re-checks the GOVFOR match before
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

/// <summary>
///     The leader picked a Custom faction authored on their own machine. The whole definition rides
///     along and is fully re-validated and clamped server-side before it is applied; the auth flag is
///     re-checked too.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyFactionSelectCustomMessage : EuiMessageBase
{
    public FactionDefinition Definition { get; }

    public InsurgencyFactionSelectCustomMessage(FactionDefinition definition)
    {
        Definition = definition;
    }
}
