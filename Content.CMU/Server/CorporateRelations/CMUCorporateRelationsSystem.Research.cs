using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.UserInterface;

namespace Content.Server.CMU14.CorporateRelations;

public sealed partial class CMUCorporateRelationsSystem
{
    private const string CorporateResearchFaction = "corporate";

    /// <summary>
    ///     Without a research licence WY can't research at all: its research terminals won't open,
    ///     and its XRF scanners refuse samples (ServerXRFScannerSystem asks <see cref="BlockResearch"/>).
    /// </summary>
    private void InitializeResearch()
    {
        SubscribeLocalEvent<ResearchDataTerminalComponent, ActivatableUIOpenAttemptEvent>(OnTerminalOpenAttempt);
    }

    private void OnTerminalOpenAttempt(Entity<ResearchDataTerminalComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || !BlockResearch(ent.Comp.Faction, ent, args.User))
            return;

        args.Cancel();
    }

    /// <summary>
    ///     True, with a popup, when <paramref name="faction"/> is WY's and its licence is revoked.
    /// </summary>
    public bool BlockResearch(string faction, EntityUid machine, EntityUid user)
    {
        if (_researchLicense || !string.Equals(faction, CorporateResearchFaction, StringComparison.OrdinalIgnoreCase))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-contracts-research-blocked"), machine, user);
        return true;
    }
}
