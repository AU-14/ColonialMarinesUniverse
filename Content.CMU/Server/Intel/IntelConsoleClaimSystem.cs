using Content.Server.AU14.Objectives;
using Content.Server.GameTicking;
using Content.Shared._CMU14.Intel;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Marines;
using Content.Shared.GameTicking;
using Content.Shared.Popups;

namespace Content.Server._CMU14.Intel;

public sealed partial class IntelConsoleClaimSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private AuObjectiveSystem _objectives = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private bool _roundEndTriggered;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClaimableIntelConsoleComponent, IntelConsoleClaimDoAfterEvent>(OnClaimDoAfter);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnClaimDoAfter(Entity<ClaimableIntelConsoleComponent> ent, ref IntelConsoleClaimDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled ||
            _roundEndTriggered ||
            !HasComp<IntelConsoleComponent>(ent) ||
            !TryComp(args.User, out MarineComponent? marine) ||
            !ent.Comp.CanCompleteClaim(marine.Faction, GetCurrentPresetId()))
        {
            return;
        }

        ent.Comp.Claimed = true;
        _roundEndTriggered = true;
        Dirty(ent);

        _popup.PopupEntity(
            Loc.GetString("cmu-intel-console-claim-complete"),
            ent.Owner,
            args.User,
            PopupType.Large);
        Logger.GetSawmill("objectives").Info(
            $"{ToPrettyString(args.User):user} claimed {ToPrettyString(ent):console} for team '{ent.Comp.ClaimingTeam}'");

        _objectives.DeclareObjectiveVictory(
            ent.Comp.ClaimingTeam,
            Loc.GetString("cmu-intel-console-claim-round-end-reason"));
    }

    private string? GetCurrentPresetId()
    {
        return _gameTicker.CurrentPreset?.ID ?? _gameTicker.Preset?.ID;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _roundEndTriggered = false;
    }
}
