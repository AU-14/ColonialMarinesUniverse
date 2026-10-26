namespace Content.Shared.CMU14.Vendors;

/// <summary>
/// Raised on an automated vendor after it hands <see cref="Item"/> to <see cref="User"/>.
/// </summary>
[ByRefEvent]
public readonly record struct CMUVendorVendedItemEvent(EntityUid User, EntityUid Item);
