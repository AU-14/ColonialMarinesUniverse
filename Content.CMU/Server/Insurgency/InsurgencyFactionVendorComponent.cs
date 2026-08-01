namespace Content.Server._AU14.Insurgency;

/// <summary>
///     Marks a vendor entity as belonging to the active INSFOR faction. When a faction is
///     applied, <see cref="InsurgencyFactionApplySystem"/> injects the sections from the
///     matching <c>FactionVendorDefinition</c> into this vendor's <c>CMAutomatedVendorComponent</c>.
///     Placed on cell-kit vendors in Phase 1b; harmless before then.
/// </summary>
[RegisterComponent]
public sealed partial class InsurgencyFactionVendorComponent : Component
{
    /// <summary>
    ///     Which vendor definition in the faction's cell-kit manifest this vendor uses,
    ///     by index into <c>CellKitManifest.VendorDefinitions</c>. Out-of-range indexes are
    ///     skipped so a mismatched map placement cannot crash the apply pass.
    /// </summary>
    [DataField]
    public int VendorIndex;
}
