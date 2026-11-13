using Content.Shared.CMU14.Vendors;

namespace Content.Shared._RMC14.Vendors;

public abstract partial class SharedCMAutomatedVendorSystem
{
    // null: an ordinary vendor, charge the buyer's own points. true: the vendor's pool paid. false: it refused.
    private bool? CMUTryPayPooledPoints(Entity<CMAutomatedVendorComponent> vendor, EntityUid actor, CMVendorSection section, CMVendorEntry entry)
    {
        if (entry.Points is not { } points)
            return null;

        var ev = new CMUVendorPooledPointsEvent(actor, section, entry, points);
        RaiseLocalEvent(vendor.Owner, ref ev);
        return ev.Handled ? ev.Paid : null;
    }
}
