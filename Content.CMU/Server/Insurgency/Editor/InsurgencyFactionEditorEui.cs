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
using Robust.Shared.Prototypes;

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
    private readonly IPrototypeManager _prototypes;
    private readonly InsurgencyEditorScope _scope;

    private List<EditorFactionEntry> _factions = new();

    public InsurgencyFactionEditorEui(
        IAdminManager admin,
        InsurgencyFactionDbSystem db,
        InsurgencyFactionApplySystem apply,
        PlatoonSpawnRuleSystem platoons,
        IPrototypeManager prototypes,
        InsurgencyEditorScope scope = InsurgencyEditorScope.Default)
    {
        _admin = admin;
        _db = db;
        _apply = apply;
        _platoons = platoons;
        _prototypes = prototypes;
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

        // The built-in vanilla CLF is seeded into the DB (once) the first time the Default editor opens, as a
        // real Default faction row marked with BuiltinOverrideOf. From then on it is an ordinary editable row
        // like any faction authored in the editor - no code-only virtual entry, so editing and saving it
        // updates in place and shows up immediately, exactly like every other faction. Only the host (Default)
        // editor seeds it; the Custom editor never touches Default rows.
        if (_scope == InsurgencyEditorScope.Default &&
            !stored.Any(s => s.Definition.Metadata.BuiltinOverrideOf == InsurgencyBuiltinFactions.VanillaClfId))
        {
            var seed = InsurgencyBuiltinFactions.VanillaClf();
            seed.Metadata.BuiltinOverrideOf = InsurgencyBuiltinFactions.VanillaClfId;
            await _db.AddFactionAsync(seed, true);
            stored = await _db.GetFactionsAsync();
        }

        _factions = stored
            // The Custom editor only ever sees Custom factions; Default (host) sees everything.
            .Where(s => _scope == InsurgencyEditorScope.Default || !s.IsDefault)
            .Select(s => new EditorFactionEntry(s.Id, s.IsDefault, s.Definition))
            .ToList();

        // Keep the built-in CLF pinned at the top where it has always sat, so editing it does not make it
        // appear to jump around the list.
        var overrideIndex = _factions.FindIndex(f => f.Definition.Metadata.BuiltinOverrideOf == InsurgencyBuiltinFactions.VanillaClfId);
        if (overrideIndex > 0)
        {
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
            case InsurgencyExportSheetMessage export:
                HandleExportSheet(export);
                break;
            case InsurgencyImportSheetMessage import:
                HandleImportSheet(import);
                break;
        }
    }

    // Builds a ready-to-fill faction spreadsheet and sends the .xlsx bytes back for the client to save.
    // A null FactionId is a blank template to hand to a player; a set id pre-fills that stored faction.
    private async void HandleExportSheet(InsurgencyExportSheetMessage msg)
    {
        FactionDefinition? existing = null;
        if (msg.FactionId is { } id)
        {
            existing = await _db.GetFactionAsync(id);
            // The Custom editor may only round-trip rows it can see (Custom ones).
            if (_scope == InsurgencyEditorScope.Custom && await IsDefaultRow(id))
                return;
        }

        var name = existing?.Metadata.Title;
        if (string.IsNullOrWhiteSpace(name))
            name = "INSFOR_Faction_Template";

        var bytes = InsforSpreadsheet.Build(_prototypes, existing);
        SendMessage(new InsurgencyExportSheetResultMessage(bytes, SanitizeFileName(name) + ".xlsx"));
    }

    // Imports a faction from a filled-in spreadsheet. Only cell values are read (no formulas/macros),
    // then the result runs through the full untrusted-payload validator (caps + unknown prototype
    // stripping) before it is stored - identical trust model to a Custom faction payload.
    private async void HandleImportSheet(InsurgencyImportSheetMessage msg)
    {
        if (msg.Workbook.Length == 0 || msg.Workbook.Length > InsforSpreadsheet.MaxWorkbookBytes)
            return;

        FactionDefinition? parsed;
        using (var stream = new System.IO.MemoryStream(msg.Workbook))
            parsed = InsforSpreadsheet.Read(stream);

        if (parsed == null)
            return;

        var def = InsurgencyFactionValidator.SanitizeCustom(parsed, _prototypes);

        // An imported faction is a brand-new row. The host editor authors Default factions; the Custom
        // editor authors Custom ones. Never carries a built-in override marker.
        var isDefault = _scope == InsurgencyEditorScope.Default;
        await _db.AddFactionAsync(def, isDefault);

        Refresh();
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');
        return name;
    }

    private async void HandleSave(InsurgencyFactionSaveMessage msg)
    {
        var def = _scope == InsurgencyEditorScope.Custom
            ? InsurgencyFactionValidator.SanitizeCustom(msg.Definition, _prototypes)
            : InsurgencyFactionValidator.Sanitize(msg.Definition);

        // The Custom editor can only author Custom factions, whatever the client claims.
        var isDefault = _scope == InsurgencyEditorScope.Default && msg.IsDefault;

        if (msg.Id is { } id)
        {
            // The Custom editor may only touch rows it can see: Custom ones.
            if (_scope == InsurgencyEditorScope.Custom && await IsDefaultRow(id))
                return;

            // The built-in CLF is now an ordinary DB row (seeded on first open), so it updates in place like
            // any faction. Re-stamp its override marker from the stored row so a save can never strip it and
            // trigger a re-seed duplicate, even if the client ever dropped the field.
            var stored = await _db.GetFactionsAsync();
            var existing = stored.FirstOrDefault(s => s.Id == id);
            if (existing.Definition?.Metadata.BuiltinOverrideOf is { } marker)
                def.Metadata.BuiltinOverrideOf = marker;

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
        // The Custom editor cannot delete host-authored Default factions.
        if (_scope == InsurgencyEditorScope.Custom && await IsDefaultRow(msg.Id))
            return;

        // Storage enforces built-in ownership, so every deletion path is protected even if another caller is added.
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
