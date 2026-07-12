using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.AU14.Round;
using Content.Server.EUI;
using Content.Server._AU14.Insurgency.Database;
using Content.Shared._AU14.Insurgency;
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
    private readonly InsurgencyEditorScope _scope;

    private List<EditorFactionEntry> _factions = new();

    public InsurgencyFactionEditorEui(
        IAdminManager admin,
        InsurgencyFactionDbSystem db,
        InsurgencyFactionApplySystem apply,
        PlatoonSpawnRuleSystem platoons,
        InsurgencyEditorScope scope = InsurgencyEditorScope.Default)
    {
        _admin = admin;
        _db = db;
        _apply = apply;
        _platoons = platoons;
        _scope = scope;
    }

    public override EuiStateBase GetNewState()
    {
        return new InsurgencyFactionEditorEuiState(_factions, _platoons.SelectedGovforPlatoon?.ID, _scope);
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
            // The Custom editor only ever sees Custom factions; Default (host) sees everything.
            .Where(s => _scope == InsurgencyEditorScope.Default || !s.IsDefault)
            .Select(s => new EditorFactionEntry(s.Id, s.IsDefault, s.Definition))
            .ToList();

        // Once the built-in vanilla CLF has been edited and saved it lives in the DB as a normal row marked
        // as its override. When that row exists, use it (it is a real, editable, updatable faction); only
        // when it does not do we fall back to showing the code-built copy as an editable starting point.
        var overrideIndex = _factions.FindIndex(f => f.Definition.Metadata.BuiltinOverrideOf == InsurgencyBuiltinFactions.VanillaClfId);
        if (overrideIndex < 0)
        {
            _factions.Insert(0, new EditorFactionEntry(
                InsurgencyBuiltinFactions.VanillaClfId, true, InsurgencyBuiltinFactions.VanillaClf()));
        }
        else if (overrideIndex > 0)
        {
            // Keep the edited built-in pinned at the top where the code copy always sat, so editing it does
            // not make it appear to jump to the bottom of the list as if a new faction had been created.
            var overrideEntry = _factions[overrideIndex];
            _factions.RemoveAt(overrideIndex);
            _factions.Insert(0, overrideEntry);
        }

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

        // The Custom editor can only author Custom factions, whatever the client claims.
        var isDefault = _scope == InsurgencyEditorScope.Default && msg.IsDefault;

        // Editing the built-in vanilla CLF upserts a single persistent override row (marked with
        // BuiltinOverrideOf) instead of spawning a fresh faction every save. After the first save that row
        // is a normal DB faction that further edits update in place, so the built-in becomes editable.
        if (msg.Id == InsurgencyBuiltinFactions.VanillaClfId)
        {
            def.Metadata.BuiltinOverrideOf = InsurgencyBuiltinFactions.VanillaClfId;

            var stored = await _db.GetFactionsAsync();
            var existing = stored.FirstOrDefault(s => s.Definition.Metadata.BuiltinOverrideOf == InsurgencyBuiltinFactions.VanillaClfId);
            if (existing != null)
                await _db.UpdateFactionAsync(existing.Id, def, true);
            else
                await _db.AddFactionAsync(def, true);
        }
        else if (msg.Id is { } id)
        {
            // The Custom editor may only touch rows it can see: Custom ones.
            if (_scope == InsurgencyEditorScope.Custom && await IsDefaultRow(id))
                return;

            await _db.UpdateFactionAsync(id, def, isDefault);
        }
        else
        {
            await _db.AddFactionAsync(def, isDefault);
        }

        Refresh();
    }

    private async void HandleDelete(InsurgencyFactionDeleteMessage msg)
    {
        // The built-in vanilla CLF is code-defined and cannot be deleted.
        if (msg.Id == InsurgencyBuiltinFactions.VanillaClfId)
            return;

        // The Custom editor cannot delete host-authored Default factions.
        if (_scope == InsurgencyEditorScope.Custom && await IsDefaultRow(msg.Id))
            return;

        await _db.DeleteFactionAsync(msg.Id);
        Refresh();
    }

    private async void HandleSelect(InsurgencyFactionSelectMessage msg)
    {
        // Applying a faction to the round stays a Default-editor (host) function.
        if (_scope == InsurgencyEditorScope.Custom)
            return;

        // Applying the built-in comes straight from code; everything else is loaded from the DB.
        if (msg.Id == InsurgencyBuiltinFactions.VanillaClfId)
        {
            _apply.ApplyFaction(InsurgencyBuiltinFactions.VanillaClf());
            return;
        }

        var def = await _db.GetFactionAsync(msg.Id);
        if (def != null)
            _apply.ApplyFaction(def);
    }

    // A row is Default when the DB says so; the client's claim is never consulted.
    private async Task<bool> IsDefaultRow(int id)
    {
        var stored = await _db.GetFactionsAsync();
        return stored.Any(s => s.Id == id && s.IsDefault);
    }

    private bool IsAllowed()
    {
        return _scope == InsurgencyEditorScope.Custom
            ? InsurgencyAuthorization.IsCustomAuthorized(_admin, Player)
            : InsurgencyAuthorization.IsAuthorized(_admin, Player);
    }
}
