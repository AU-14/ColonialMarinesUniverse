using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.AU14.Round;
using Content.Server.EUI;
using Content.Server._AU14.Insurgency.Database;
using Content.Shared._AU14.Insurgency.Editor;
using Content.Shared.Eui;

namespace Content.Server._AU14.Insurgency.Editor;

/// <summary>
///     Server side of the Default-faction editor. Loads stored factions from the DB, pushes them to
///     the client, and handles create / update / delete / select messages. Every message re-checks
///     authorization and runs the definition through <see cref="InsurgencyFactionValidator"/> before
///     it touches the DB, so nothing the client sends is trusted as-is.
///
///     Named to match its client counterpart so the EUI manager pairs them by type name.
/// </summary>
public sealed class InsurgencyFactionEditorEui : BaseEui
{
    private readonly IAdminManager _admin;
    private readonly InsurgencyFactionDbSystem _db;
    private readonly InsurgencyFactionApplySystem _apply;
    private readonly PlatoonSpawnRuleSystem _platoons;

    private List<EditorFactionEntry> _factions = new();

    public InsurgencyFactionEditorEui(
        IAdminManager admin,
        InsurgencyFactionDbSystem db,
        InsurgencyFactionApplySystem apply,
        PlatoonSpawnRuleSystem platoons)
    {
        _admin = admin;
        _db = db;
        _apply = apply;
        _platoons = platoons;
    }

    public override EuiStateBase GetNewState()
    {
        return new InsurgencyFactionEditorEuiState(_factions, _platoons.SelectedGovforPlatoon?.ID);
    }

    public override void Opened()
    {
        base.Opened();
        Refresh();
    }

    // async void: fire-and-forget refresh, standard for EUI DB round-trips. StateDirty pushes the
    // fresh list to the client once the query returns.
    private async void Refresh()
    {
        if (!IsAllowed())
            return;

        var stored = await _db.GetFactionsAsync();
        _factions = stored
            .Select(s => new EditorFactionEntry(s.Id, s.IsDefault, s.Definition))
            .ToList();
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!IsAllowed())
            return;

        switch (msg)
        {
            case InsurgencyFactionSaveMessage save:
                HandleSave(save);
                break;
            case InsurgencyFactionDeleteMessage del:
                HandleDelete(del);
                break;
            case InsurgencyFactionSelectMessage sel:
                HandleSelect(sel);
                break;
            case InsurgencyFactionRefreshMessage:
                Refresh();
                break;
        }
    }

    private async void HandleSave(InsurgencyFactionSaveMessage msg)
    {
        var def = InsurgencyFactionValidator.Sanitize(msg.Definition);

        if (msg.Id is { } id)
            await _db.UpdateFactionAsync(id, def, msg.IsDefault);
        else
            await _db.AddFactionAsync(def, msg.IsDefault);

        Refresh();
    }

    private async void HandleDelete(InsurgencyFactionDeleteMessage msg)
    {
        await _db.DeleteFactionAsync(msg.Id);
        Refresh();
    }

    private async void HandleSelect(InsurgencyFactionSelectMessage msg)
    {
        var def = await _db.GetFactionAsync(msg.Id);
        if (def != null)
            _apply.ApplyFaction(def);
    }

    private bool IsAllowed()
    {
        return InsurgencyAuthorization.IsAuthorized(_admin, Player);
    }
}
