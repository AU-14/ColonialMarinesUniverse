using Content.Server.CMU14.Round;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.CMU14.Insurgency.Selection;
using Content.Shared.GameTicking;

namespace Content.Server.CMU14.Insurgency.Selection;

/// <summary>Offers the faction ballot to each insurgent who spawns before voting closes.</summary>
public sealed partial class InsurgencyLeaderSpawnSystem : EntitySystem
{
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private InsurgencyFactionSelectionSystem _selection = default!;
    [Dependency] private InsurgencyFactionApplySystem _apply = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;

    [Dependency] private GameTicker _ticker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeNetworkEvent<InsurgencyReopenFactionSelectEvent>(OnReopenRequest);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if ((_ticker.CurrentPreset ?? _ticker.Preset)?.ID != "Insurgency" ||
            ev.JobId?.StartsWith("AU14JobCLF", StringComparison.Ordinal) != true || !HasComp<CLFMemberComponent>(ev.Mob))
            return;

        if (_apply.GetActiveFaction() != null || !_selection.TryStartBallot())
            return;

        EnsureComp<InsurgencyPendingFactionSelectionComponent>(ev.Mob);

        OpenSelector(ev.Player);
    }

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
        var ballot = new InsurgencyFactionSelectEui(_selection, _platoons);
        _eui.OpenEui(ballot, session);
    }
}
