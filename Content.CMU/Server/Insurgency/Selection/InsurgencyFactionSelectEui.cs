using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.Round;
using Content.Server.EUI;
using Content.Server.CMU14.Insurgency.Database;
using Content.Shared.CMU14.Insurgency;
using Content.Shared.CMU14.Insurgency.Selection;
using Content.Shared.Eui;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Insurgency.Selection;

/// <summary>
///     Shows eligible faction choices and registers the player's ballot.
/// </summary>
public sealed class InsurgencyFactionSelectEui : BaseEui
{
    private readonly IAdminManager _admin;
    private readonly IPrototypeManager _prototypes;
    private readonly InsurgencyFactionDbSystem _db;
    private readonly InsurgencyFactionSelectionSystem _selection;
    private readonly PlatoonSpawnRuleSystem _platoons;

    private List<DefaultFactionOption> _defaults = new();

    public InsurgencyFactionSelectEui(
        IAdminManager admin,
        IPrototypeManager prototypes,
        InsurgencyFactionDbSystem db,
        InsurgencyFactionSelectionSystem selection,
        PlatoonSpawnRuleSystem platoons)
    {
        _admin = admin;
        _prototypes = prototypes;
        _db = db;
        _selection = selection;
        _platoons = platoons;
    }

    public override EuiStateBase GetNewState()
    {
        return new InsurgencyFactionSelectEuiState(_defaults, CanUseCustom(), _platoons.SelectedGovforPlatoon is { } p ? Loc.GetString(p.Name) : null);
    }

    public override void Opened()
    {
        base.Opened();
        _selection.BallotClosed += Close;
        _selection.CandidatesChanged += Refresh;
        Refresh();
    }

    public override void Closed()
    {
        _selection.BallotClosed -= Close;
        _selection.CandidatesChanged -= Refresh;
        base.Closed();
    }

    private async void Refresh()
    {
        var govfor = _platoons.SelectedGovforPlatoon?.ID;
        List<InsurgencyFactionDbSystem.StoredFaction> stored;
        try
        {
            stored = await _db.GetFactionsAsync();
        }
        catch (Exception error)
        {
            Logger.Error($"Unable to load INSFOR ballot options: {error}");
            stored = [];
        }

        var options = new List<DefaultFactionOption>();

        // The built-in vanilla CLF faction is always offered first and always opposes every GOVFOR - unless
        // it has been edited and saved, in which case its persistent DB override row (below) stands in for
        // it, so we skip the code copy to avoid listing the same faction twice.
        var hasOverride = stored.Any(s => s.Definition.Metadata.BuiltinOverrideOf == InsurgencyBuiltinFactions.VanillaClfId);
        if (!hasOverride)
        {
            var vanilla = InsurgencyBuiltinFactions.VanillaClf();
            options.Add(new DefaultFactionOption(
                InsurgencyBuiltinFactions.VanillaClfId,
                vanilla.Metadata.Title,
                vanilla.Metadata.Description,
                vanilla.Metadata.RoleplayText,
                vanilla.Metadata.FlagEntity?.Id,
                vanilla.Metadata.StatusIcon?.Id,
                CellKitEntities(vanilla),
                true));
        }

        options.AddRange(stored
            .Where(s => s.IsDefault)
            .Select(s =>
            {
                var meta = s.Definition.Metadata;
                // The built-in CLF override opposes every GOVFOR, like the code copy always did.
                var opposes = govfor != null &&
                              (meta.BuiltinOverrideOf == InsurgencyBuiltinFactions.VanillaClfId ||
                               meta.OpposedGovforFactions.Any(g => string.Equals(g, govfor, StringComparison.OrdinalIgnoreCase)));
                return new DefaultFactionOption(
                    s.Id,
                    meta.Title,
                    meta.Description,
                    meta.RoleplayText,
                    meta.FlagEntity?.Id,
                    meta.StatusIcon?.Id,
                    CellKitEntities(s.Definition),
                    opposes);
            }));

        foreach (var (id, definition) in _selection.CustomCandidates)
        {
            var meta = definition.Metadata;
            options.Add(new DefaultFactionOption(id, meta.Title, meta.Description, meta.RoleplayText,
                meta.FlagEntity?.Id, meta.StatusIcon?.Id, CellKitEntities(definition), true));
        }
        _defaults = options;

        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case CMUInsurgencyExclusionsMessage exclusions:
                _selection.SetExclusions(Player, exclusions.Ids);
                break;
            case InsurgencyFactionSelectDefaultMessage def:
                HandleSelectDefault(def);
                break;
            case InsurgencyFactionSelectCustomMessage custom:
                HandleSelectCustom(custom);
                break;
        }
    }

    private void HandleSelectDefault(InsurgencyFactionSelectDefaultMessage msg)
    {
        if (_defaults.Any(option => option.Id == msg.Id && option.Opposes) && _selection.Vote(Player, msg.Id))
            Close();
    }

    private void HandleSelectCustom(InsurgencyFactionSelectCustomMessage msg)
    {
        // Custom factions are gated by the auth flag, and the whole client-authored payload is
        // clamped and stripped of unknown prototype ids before it is ever applied.
        if (!CanUseCustom())
            return;

        var def = InsurgencyFactionValidator.SanitizeCustom(msg.Definition, _prototypes);
        _selection.Nominate(Player, def);
        Close();
    }

    // How many cell-kit sprites the popup previews. A generous cap so the grid never explodes on a
    // faction with huge vendors, while still showing the bulk of what a normal cell deploys.
    private const int MaxCellKitPreview = 80;

    // Gathers what a faction's cell kit puts in play, for the sprite preview: the free-placed entities,
    // each vendor's base model, then the items its vendors stock. Deduplicated, order preserved, capped.
    private static List<string> CellKitEntities(Content.Shared.CMU14.Insurgency.FactionDefinition def)
    {
        var seen = new HashSet<string>();
        var result = new List<string>();

        void Add(string? id)
        {
            if (result.Count >= MaxCellKitPreview || string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                return;
            result.Add(id);
        }

        foreach (var placeable in def.CellKit.PlaceableEntities)
            Add(placeable.Id);

        foreach (var vendor in def.CellKit.VendorDefinitions)
        {
            Add(vendor.BaseModel.Id);
            foreach (var section in vendor.Sections)
            foreach (var entry in section.Entries)
                Add(entry.Id.Id);
        }

        return result;
    }

    private bool CanUseCustom() => InsurgencyAuthorization.IsAuthorized(_admin, Player);
}
