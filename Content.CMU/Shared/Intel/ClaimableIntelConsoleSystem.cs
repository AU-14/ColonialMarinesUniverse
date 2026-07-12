using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Marines;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._CMU14.Intel;

public sealed partial class ClaimableIntelConsoleSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClaimableIntelConsoleComponent, InteractHandEvent>(OnInteractHand,
            before: [typeof(IntelSystem)]);
        SubscribeLocalEvent<ClaimableIntelConsoleComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractHand(Entity<ClaimableIntelConsoleComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled ||
            !TryComp(args.User, out MarineComponent? marine) ||
            !ent.Comp.CanStartClaim(marine.Faction))
        {
            return;
        }

        // Claiming takes precedence over uploading intel to an enemy console.
        args.Handled = true;
        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.ClaimTime,
            new IntelConsoleClaimDoAfterEvent(),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupPredicted(
                Loc.GetString("cmu-intel-console-claim-start"),
                ent.Owner,
                args.User,
                PopupType.Medium);
        }
    }

    private void OnExamined(Entity<ClaimableIntelConsoleComponent> ent, ref ExaminedEvent args)
    {
        if (TryComp(args.Examiner, out MarineComponent? marine) && ent.Comp.CanStartClaim(marine.Faction))
            args.PushMarkup(Loc.GetString("cmu-intel-console-claim-examine"));
    }
}
