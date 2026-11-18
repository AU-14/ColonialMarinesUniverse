using Content.Shared.Inventory.Events;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Xenomorphs.Pathogen.Walker;

public sealed partial class CMUPathogenWalkerMarkerSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPathogenWalkerMarkerComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<CMUPathogenWalkerMarkerComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnUnequipAttempt(Entity<CMUPathogenWalkerMarkerComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        args.Cancel();
    }

    // only a forced unequip gets here (gibs, cures). a loose bloom could be picked up and worn
    // by anyone for free night vision, so it rots away instead
    private void OnGotUnequipped(Entity<CMUPathogenWalkerMarkerComponent> ent, ref GotUnequippedEvent args)
    {
        if (_net.IsServer)
            QueueDel(ent);
    }
}
