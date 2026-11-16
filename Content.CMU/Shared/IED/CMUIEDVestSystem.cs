using Content.Shared.Humanoid;
using Content.Shared.Inventory.Events;

namespace Content.Shared.CMU14.IED;

public sealed class CMUIEDVestSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUIEDVestComponent, BeingEquippedAttemptEvent>(OnBeingEquippedAttempt);
    }

    private void OnBeingEquippedAttempt(Entity<CMUIEDVestComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled || HasComp<HumanoidProfileComponent>(args.EquipTarget))
            return;

        args.Reason = "cmu-ied-vest-humanoid-only";
        args.Cancel();
    }
}
