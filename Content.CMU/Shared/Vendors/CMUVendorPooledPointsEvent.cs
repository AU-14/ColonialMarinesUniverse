using Content.Shared._RMC14.Vendors;

namespace Content.Shared.CMU14.Vendors;

/// <summary>
/// Raised on an automated vendor, on the server, when a priced entry is about to be paid for. A vendor that sells
/// from a shared pool sets <see cref="Handled"/> and takes the price from its pool itself: <see cref="Paid"/> says
/// whether the purchase goes ahead. Nothing answers this for an ordinary vendor, which then charges the buyer's
/// own points as before.
/// </summary>
[ByRefEvent]
public record struct CMUVendorPooledPointsEvent(EntityUid User, CMVendorSection Section, CMVendorEntry Entry, int Points)
{
    /// <summary>
    /// This vendor pays from a pool. The buyer's own points are left alone whether the purchase is paid or refused.
    /// </summary>
    public bool Handled;

    /// <summary>
    /// The pool paid. Only read when <see cref="Handled"/> is set. The handler tells the buyer why when it refuses.
    /// </summary>
    public bool Paid;
}
