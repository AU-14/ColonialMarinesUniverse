using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.EUI;
using Content.Shared.CMU14.Insurgency;
using Content.Shared.CMU14.Insurgency.Selection;
using Content.Shared.Eui;

namespace Content.Server.CMU14.Insurgency.Selection;

/// <summary>
///     Shows eligible faction choices and registers the player's ballot.
/// </summary>
public sealed class InsurgencyFactionSelectEui(
    InsurgencyFactionSelectionSystem selection,
    PlatoonSpawnRuleSystem platoons) : BaseEui
{
    public override EuiStateBase GetNewState()
    {
        var options = selection.Factions.OrderBy(f => f.BallotId).Select(faction =>
        {
            var def = faction.Definition;
            var meta = def.Metadata;
            return new DefaultFactionOption(faction.BallotId, meta.Title, meta.Description, meta.RoleplayText,
                meta.FlagEntity?.Id, meta.StatusIcon?.Id, CellKitEntities(def), selection.IsAvailable(faction));
        }).ToList();
        return new InsurgencyFactionSelectEuiState(options,
            platoons.SelectedGovforPlatoon is { } p ? Loc.GetString(p.Name) : null, selection.Deadline);
    }

    public override void Opened()
    {
        base.Opened();
        selection.BallotClosed += Close;
        StateDirty();
    }

    public override void Closed()
    {
        selection.BallotClosed -= Close;
        base.Closed();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case CMUInsurgencyExclusionsMessage exclusions:
                selection.SetExclusions(Player, exclusions.Ids);
                break;
            case InsurgencyFactionSelectDefaultMessage def:
                if (selection.Vote(Player, def.Id))
                    Close();
                break;
        }
    }

    // How many cell-kit sprites the popup previews. A generous cap so the grid never explodes on a
    // faction with huge vendors, while still showing the bulk of what a normal cell deploys.
    private const int MaxCellKitPreview = 80;

    // Gathers what a faction's cell kit puts in play, for the sprite preview: the free-placed entities,
    // each vendor's base model, then the items its vendors stock. Deduplicated, order preserved, capped.
    private static List<string> CellKitEntities(FactionDefinition def)
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
}
