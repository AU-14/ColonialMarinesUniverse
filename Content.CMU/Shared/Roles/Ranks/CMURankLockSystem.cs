using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared.CMU14.Roles.Ranks;

public sealed class CMURankLockSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMURankLockComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<CMURankLockComponent, BoundUserInterfaceMessageAttempt>(OnMessageAttempt);
    }

    public bool HasAccess(Entity<CMURankLockComponent?> ent, EntityUid user)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return true;

        return TryComp<RankComponent>(user, out var rank) &&
               rank.Rank is { } rankId &&
               ent.Comp.AllowedRanks.Contains(rankId);
    }

    private void OnOpenAttempt(Entity<CMURankLockComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || HasAccess(ent.AsNullable(), args.User))
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupClient(Loc.GetString(ent.Comp.DeniedPopup), ent, args.User);
    }

    // Server-side backstop: a client that skips the open check still can't send console actions.
    private void OnMessageAttempt(Entity<CMURankLockComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (!args.Cancelled && !HasAccess(ent.AsNullable(), args.Actor))
            args.Cancel();
    }
}
