using Content.Shared.Power.EntitySystems;

namespace Content.Client.Power;

public sealed partial class ActivatableUIRequiresPowerSystem : SharedActivatableUIRequiresPowerSystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    protected override void OnActivate(Entity<ActivatableUIRequiresPowerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || this.IsPowered(ent.Owner, EntityManager))
        {
            return;
        }

        _popup.PopupClient(Loc.GetString("base-computer-ui-component-not-powered", ("machine", ent.Owner)), args.User, args.User);
        args.Cancel();
    }
}
