using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Whitelist;

namespace Content.Shared.CMU14.Items;

public sealed partial class CMUPickupBlockerSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPickupBlockerComponent, PickupAttemptEvent>(OnPickupAttempt);
    }

    private void OnPickupAttempt(Entity<CMUPickupBlockerComponent> ent, ref PickupAttemptEvent args)
    {
        if (!_whitelist.IsValid(ent.Comp.Blocked, args.Item))
            return;

        args.Cancel();

        if (args.ShowPopup)
            _popup.PopupClient(Loc.GetString(ent.Comp.BlockedPopup, ("item", args.Item)), ent, ent, PopupType.SmallCaution);
    }
}
