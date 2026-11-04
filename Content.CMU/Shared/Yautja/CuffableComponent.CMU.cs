using Content.Shared.Whitelist;

namespace Content.Shared.Cuffs.Components;

public sealed partial class CuffableComponent
{
    /// <summary>Specialized bodies can require specialized restraints.</summary>
    [DataField]
    public EntityWhitelist? CuffsWhitelist;
}
