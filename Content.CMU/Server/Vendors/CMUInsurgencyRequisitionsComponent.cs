using Content.Shared._RMC14.Vendors;

namespace Content.Server.CMU14.Vendors;

/// <summary>
/// Requisitions stock whose night-vision scope attachments are unavailable during Insurgency.
/// </summary>
[RegisterComponent]
public sealed partial class CMUInsurgencyRequisitionsComponent : Component
{
    public readonly List<(CMVendorSection Section, int Index, CMVendorEntry Entry)> HiddenScopes = new();
}
