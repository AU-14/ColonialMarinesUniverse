using Content.Shared._RMC14.Xenonids;
using Content.Shared.UserInterface;

namespace Content.Shared.CMU14.Nuke;

public sealed class CMUNukeUIRestrictionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUNukeComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
    }

    private void OnUiOpenAttempt(Entity<CMUNukeComponent> nuke, ref ActivatableUIOpenAttemptEvent args)
    {
        if (HasComp<XenoComponent>(args.User))
            args.Cancel();
    }
}
