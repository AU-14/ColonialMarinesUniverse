using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.AU14.Round;
using Content.Server.EUI;
using Content.Server._AU14.Insurgency.Database;
using Content.Shared._AU14.Insurgency;
using Content.Shared._AU14.Insurgency.Selection;
using Content.Shared.Eui;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Insurgency.Selection;

/// <summary>
///     Server side of the CLF-leader faction selection popup. Loads the round's Default factions,
///     marks which oppose the chosen GOVFOR platoon, and reports whether the player may pick a Custom
///     faction. Every pick is re-validated here: Default picks re-check the GOVFOR match, Custom picks
///     re-check the auth flag and run the whole payload through the server validator before applying.
///
///     Shares its short type name with the client EUI so the EUI manager pairs them.
/// </summary>
public sealed class InsurgencyFactionSelectEui : BaseEui
{
    private readonly IAdminManager _admin;
    private readonly IPrototypeManager _prototypes;
    private readonly InsurgencyFactionDbSystem _db;
    private readonly InsurgencyFactionSelectionSystem _selection;
    private readonly InsurgencyFactionApplySystem _apply;
    private readonly PlatoonSpawnRuleSystem _platoons;

    private List<DefaultFactionOption> _defaults = new();

    public InsurgencyFactionSelectEui(
        IAdminManager admin,
        IPrototypeManager prototypes,
        InsurgencyFactionDbSystem db,
        InsurgencyFactionSelectionSystem selection,
        InsurgencyFactionApplySystem apply,
        PlatoonSpawnRuleSystem platoons)
    {
        _admin = admin;
        _prototypes = prototypes;
        _db = db;
        _selection = selection;
        _apply = apply;
        _platoons = platoons;
    }

    public override EuiStateBase GetNewState()
    {
        return new InsurgencyFactionSelectEuiState(_defaults, CanUseCustom(), _platoons.SelectedGovforPlatoon?.Name);
    }

    public override void Opened()
    {
        base.Opened();
        Refresh();
    }

    // async void: fire-and-forget DB load, standard for EUI round-trips. StateDirty pushes the list
    // once the query returns.
    private async void Refresh()
    {
        var govfor = _platoons.SelectedGovforPlatoon?.ID;
        var stored = await _db.GetFactionsAsync();

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

        _defaults = options;

        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case InsurgencyFactionSelectDefaultMessage def:
                HandleSelectDefault(def);
                break;
            case InsurgencyFactionSelectCustomMessage custom:
                HandleSelectCustom(custom);
                break;
        }
    }

    private async void HandleSelectDefault(InsurgencyFactionSelectDefaultMessage msg)
    {
        // The built-in vanilla CLF faction is not in the DB: apply it directly. It opposes every GOVFOR
        // so there is no match to re-check.
        if (msg.Id == InsurgencyBuiltinFactions.VanillaClfId)
        {
            _apply.ApplyFaction(InsurgencyBuiltinFactions.VanillaClf());
            Close();
            return;
        }

        // Selection system re-checks the GOVFOR match server-side before applying.
        if (await _selection.SelectDefaultFactionAsync(msg.Id))
            Close();
    }

    private void HandleSelectCustom(InsurgencyFactionSelectCustomMessage msg)
    {
        // Custom factions are gated by the auth flag, and the whole client-authored payload is
        // clamped and stripped of unknown prototype ids before it is ever applied.
        if (!CanUseCustom())
            return;

        var def = InsurgencyFactionValidator.SanitizeCustom(msg.Definition, _prototypes);
        _apply.ApplyFaction(def);
        Close();
    }

    // How many cell-kit sprites the popup previews. A generous cap so the grid never explodes on a
    // faction with huge vendors, while still showing the bulk of what a normal cell deploys.
    private const int MaxCellKitPreview = 80;

    // Gathers what a faction's cell kit puts in play, for the sprite preview: the free-placed entities,
    // each vendor's base model, then the items its vendors stock. Deduplicated, order preserved, capped.
    private static List<string> CellKitEntities(Content.Shared._AU14.Insurgency.FactionDefinition def)
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
