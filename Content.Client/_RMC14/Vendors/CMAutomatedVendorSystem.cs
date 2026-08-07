using Content.Shared._RMC14.Medical.Refill;
using Content.Shared._CMU14.Round.Objectives.Component;
using Content.Shared._RMC14.Vendors;

namespace Content.Client._RMC14.Vendors;

public sealed class CMAutomatedVendorSystem : SharedCMAutomatedVendorSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMAutomatedVendorComponent, AfterAutoHandleStateEvent>(OnRefresh);
        SubscribeLocalEvent<CMSolutionRefillerComponent, AfterAutoHandleStateEvent>(OnRefresh);
        SubscribeLocalEvent<CMUObjectiveMasterComponent, AfterAutoHandleStateEvent>(OnMasterPointsChanged);
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

    private void OnMasterPointsChanged(Entity<CMUObjectiveMasterComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var vendors = EntityQueryEnumerator<CMAutomatedVendorComponent, UserInterfaceComponent>();
        while (vendors.MoveNext(out _, out var vendor, out var ui))
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
