using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared.CMU14.Pool;

public sealed partial class SharedCMUPoolInteractionSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popups = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPoolTableComponent, ActivatableUIOpenAttemptEvent>(OnAttemptOpen);
        SubscribeLocalEvent<CMUPoolTableComponent, BoundUserInterfaceMessageAttempt>(OnUiMessageAttempt);
    }

    public bool HasActiveStick(EntityUid user)
        => _hands.TryGetActiveItem(user, out var item)
           && TryComp<CMUPoolStickComponent>(item.Value, out var stick)
           && !stick.Spent;

    private void OnAttemptOpen(Entity<CMUPoolTableComponent> table, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryComp<ActivatableUIComponent>(table.Owner, out var activation)
            && activation.CurrentSingleUser is { } player && player != args.User)
        {
            args.Cancel();
            if (!args.Silent)
                _popups.PopupEntity(Loc.GetString("machine-already-in-use", ("machine", table.Owner)), table.Owner, args.User);
        }
        else if (!HasActiveStick(args.User))
        {
            args.Cancel();
            if (!args.Silent)
                _popups.PopupEntity(Loc.GetString("cmu-pool-need-stick"), table.Owner, args.User);
        }
    }

    private void OnUiMessageAttempt(Entity<CMUPoolTableComponent> table, ref BoundUserInterfaceMessageAttempt args)
    {
        if (!Equals(args.UiKey, CMUPoolUiKey.FreePlay) || args.Message is not OpenBoundInterfaceMessage)
            return;

        if (!TryComp<ActivatableUIComponent>(table.Owner, out var activation)
            || activation.CurrentSingleUser != args.Actor || !HasActiveStick(args.Actor))
            args.Cancel();
    }
}
