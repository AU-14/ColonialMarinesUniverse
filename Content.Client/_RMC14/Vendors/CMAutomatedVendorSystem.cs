using Content.Shared._RMC14.Medical.Refill;
using Content.Shared._RMC14.Vendors;
using Content.Shared.AU14.Objectives;
using Robust.Client.UserInterface;

namespace Content.Client._RMC14.Vendors;

public sealed partial class CMAutomatedVendorSystem : SharedCMAutomatedVendorSystem
{
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMAutomatedVendorComponent, AfterAutoHandleStateEvent>(OnRefresh);
        SubscribeLocalEvent<CMSolutionRefillerComponent, AfterAutoHandleStateEvent>(OnRefresh);

        // When the server pushes updated win-point values, refresh every open objective-point vendor BUI.
        SubscribeLocalEvent<ObjectiveMasterComponent, AfterAutoHandleStateEvent>(OnMasterPointsChanged);
    }

    private void OnRefresh<T>(Entity<T> ent, ref AfterAutoHandleStateEvent args) where T : IComponent?
    {
        if (!TryComp(ent, out UserInterfaceComponent? ui))
            return;

        foreach (var bui in ui.ClientOpenInterfaces.Values)
        {
            if (bui is CMAutomatedVendorBui vendorUi)
                vendorUi.Refresh();
        }
    }

    private void OnMasterPointsChanged(Entity<ObjectiveMasterComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var vendors = EntityQueryEnumerator<CMAutomatedVendorComponent, UserInterfaceComponent>();
        while (vendors.MoveNext(out var uid, out var vendor, out var ui))
        {
            if (!vendor.UseObjectivePoints)
                continue;

            foreach (var bui in ui.ClientOpenInterfaces.Values)
            {
                if (bui is CMAutomatedVendorBui vendorUi)
                    vendorUi.Refresh();
            }
        }
    }
}
