using Content.Shared.Administration.Managers;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Marines;

namespace Content.Shared._AU14.Callsigns;

// faction access control for the comms net directory
// this lives in shared rather than on the server system because ActivatableUISystem is
// shared and the client predicts the open: a server-only refusal lets the window appear
// for a tick before the correction lands, which is long enough to read a name off it
public sealed class SharedAU14CallsignConsoleSystem : EntitySystem
{
    [Dependency] private ISharedAdminManager _admin = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AU14CallsignConsoleComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<AU14CallsignConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || CanView(ent, args.User))
            return;

        _popup.PopupClient(Loc.GetString("au14-callsign-console-wrong-faction"), ent.Owner, args.User);
        args.Cancel();
    }

    // the roster is signals intelligence: a captured enemy terminal or manpack must not
    // hand over the other side's net
    // admins are exempt only while aghosted
    public bool CanView(Entity<AU14CallsignConsoleComponent> ent, EntityUid actor)
    {
        if (TryComp(actor, out GhostComponent? ghost) && ghost.CanGhostInteract && _admin.IsAdmin(actor))
            return true;

        return GetActorFaction(actor) == ent.Comp.Faction;
    }

    public string? GetActorFaction(EntityUid actor)
    {
        return HasComp<CLFMemberComponent>(actor)
            ? "clf"
            : CompOrNull<MarineComponent>(actor)?.Faction;
    }
}
