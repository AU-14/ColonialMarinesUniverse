using Content.Server.Administration.Managers;
using Content.Server.AU14.Round;
using Content.Server.EUI;
using Content.Server._AU14.Insurgency.Database;
using Content.Shared._AU14.Insurgency.Selection;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Insurgency.Selection;

/// <summary>
///     Opens the faction selection popup for the CLF cell leader when they spawn, so the leader picks
///     the round's faction after spawn (which is why loadouts arrive later via "A Package"). Only the
///     leader is prompted, and only while no faction has been applied yet, so a faction is chosen once
///     per round.
///
///     Event-driven: one subscription to the spawn-complete event, no polling.
/// </summary>
public sealed class InsurgencyLeaderSpawnSystem : EntitySystem
{
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private InsurgencyFactionDbSystem _db = default!;
    [Dependency] private InsurgencyFactionSelectionSystem _selection = default!;
    [Dependency] private InsurgencyFactionApplySystem _apply = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;

    // ---------------------------------------------------------------------
    // Tunable. The job that gets to pick the round's faction. Change here if the picking role moves.
    // ---------------------------------------------------------------------
    private const string LeaderJobId = "AU14JobCLFCellLeader";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeNetworkEvent<InsurgencyReopenFactionSelectEvent>(OnReopenRequest);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!string.Equals(ev.JobId, LeaderJobId, System.StringComparison.OrdinalIgnoreCase))
            return;

        // A faction was already chosen for this round (admin-applied, or a re-spawned leader): do not
        // prompt again.
        if (_apply.GetActiveFaction() != null)
            return;

        // Mark the leader so their client shows the reopen button if they close the popup.
        EnsureComp<InsurgencyPendingFactionSelectionComponent>(ev.Mob);

        OpenSelector(ev.Player);
    }

    // The leader closed the popup and pressed the in-viewport reopen button. Re-validate before trusting
    // it: they must still be the pending leader and no faction can have been applied since.
    private void OnReopenRequest(InsurgencyReopenFactionSelectEvent ev, EntitySessionEventArgs args)
    {
        if (_apply.GetActiveFaction() != null)
            return;

        if (args.SenderSession.AttachedEntity is not { } mob)
            return;

        if (!HasComp<InsurgencyPendingFactionSelectionComponent>(mob))
            return;

        OpenSelector(args.SenderSession);
    }

    private void OpenSelector(Robust.Shared.Player.ICommonSession session)
    {
        var editor = new InsurgencyFactionSelectEui(_admin, _prototypes, _db, _selection, _apply, _platoons);
        _eui.OpenEui(editor, session);
    }
}
