using Content.Shared.Access.Components;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared.Access.Systems;
public sealed partial class ActivatableUIRequiresAccessSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminManager _admin = default!; // CMU14: admin bypass

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActivatableUIRequiresAccessComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
    }

    private void OnUIOpenAttempt(Entity<ActivatableUIRequiresAccessComponent> activatableUI, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // CMU14: admins bypass the access gate
        if (_admin.HasAdminFlag(args.User, AdminFlags.Admin))
            return;

        if (!_access.IsAllowed(args.User, activatableUI))
        {
            args.Cancel();
            if (activatableUI.Comp.PopupMessage != null && !args.Silent)
                _popup.PopupEntity(Loc.GetString(activatableUI.Comp.PopupMessage), activatableUI, args.User);
        }
    }
}

