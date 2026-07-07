using Content.Shared._RMC14.Vendors;
using Content.Shared.AU14.Objectives;
using Content.Server.AU14.Objectives;
using Robust.Server.GameObjects;

namespace Content.Server._RMC14.Vendors;

public sealed partial class CMAutomatedVendorSystem : SharedCMAutomatedVendorSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private AuObjectiveSystem _objectiveSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMAutomatedVendorComponent, ComponentStartup>(OnVendorStartup);
    }

    private void OnVendorStartup(EntityUid uid, CMAutomatedVendorComponent vendor, ComponentStartup args)
    {
        // Initialize the cached win-point balance for objective-point vendors
        if (!vendor.UseObjectivePoints)
            return;

        vendor.CachedFactionWinPoints = _objectiveSystem.GetWinPoints(vendor.Faction).current;
        Dirty(uid, vendor);
    }

    protected override void OnVendBui(Entity<CMAutomatedVendorComponent> vendor, ref CMVendorVendBuiMsg args)
    {
        base.OnVendBui(vendor, ref args);

        var msg = new CMVendorRefreshBuiMsg();
        _ui.ServerSendUiMessage(vendor.Owner, args.UiKey, msg, args.Actor);
    }
}
