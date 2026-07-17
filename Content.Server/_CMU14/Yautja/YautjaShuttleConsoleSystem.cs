using Content.Server.Shuttles.Systems;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared._CMU14.Yautja;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaShuttleConsoleSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaShuttleConsoleComponent, ActivatableUIOpenAttemptEvent>(
            OnOpenAttempt,
            before: [typeof(ShuttleConsoleSystem)]);
    }

    private void OnOpenAttempt(Entity<YautjaShuttleConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (HasComp<YautjaComponent>(args.User) || HasComp<YautjaTechAuthorizedComponent>(args.User))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("cmu-yautja-shuttle-console-denied"), ent, args.User);
    }
}
