using Content.Shared.Inventory.Events;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;

public sealed partial class CMUPathogenWalkerMarkerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPathogenWalkerMarkerComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
    }

    private void OnUnequipAttempt(Entity<CMUPathogenWalkerMarkerComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        args.Cancel();
    }
}