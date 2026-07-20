using Content.Server.EUI;
using Content.Shared._AU14.Insurgency;
using Content.Shared._AU14.Insurgency.Selection;
using Content.Shared.Eui;

namespace Content.Server._AU14.Insurgency.Selection;

/// <summary>
///     Server side of the faction reveal popup. One is opened per cell member when a faction is
///     applied, carrying that faction's presentation data. Read-only: it has no messages beyond the
///     built-in close. Shares its short type name with the client EUI.
/// </summary>
public sealed class InsurgencyFactionRevealEui : BaseEui
{
    private readonly FactionDefinition _definition;

    public InsurgencyFactionRevealEui(FactionDefinition definition)
    {
        _definition = definition;
    }

    // EUIs only push a state when flagged dirty; without this the client never gets the data and the
    // popup window is never built.
    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        var meta = _definition.Metadata;
        return new InsurgencyFactionRevealEuiState(
            meta.Title,
            meta.Description,
            meta.RoleplayText,
            meta.FlagEntity?.Id,
            meta.StatusIcon?.Id);
    }
}
