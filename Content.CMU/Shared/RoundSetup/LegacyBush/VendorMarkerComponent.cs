namespace Content.Shared._CMU14.RoundSetup.LegacyBush;

using Content.Shared.AU14.util;
using Robust.Shared.Prototypes;

/// <summary>
/// Marks a USS Bush map location where the legacy platoon setup placed a ship entity.
/// The current compatibility resolver uses <see cref="Replacement"/> to materialize the
/// default UNMC equivalent without restoring the removed AU platoon framework.
/// </summary>
[RegisterComponent]
public sealed partial class VendorMarkerComponent : Component
{
    [DataField]
    public bool Govfor;

    [DataField]
    public bool Opfor;

    [DataField("dropship")]
    public bool DropShip;

    [DataField]
    public bool Ship;

    [DataField]
    public PlatoonMarkerClass Class;

    [DataField]
    public EntProtoId? Replacement;

    [DataField]
    public bool PreserveName;
}
