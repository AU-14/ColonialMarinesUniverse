using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Mycotoxin;

/// <summary>
/// Marker for wearable items (masks, helmets) that fully or partially
/// protect against Mycotoxin exposure, e.g. gasmasks and CBRN gear.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MycotoxinProtectionComponent : Component
{
    /// <summary>
    /// If true, blocks exposure completely while worn.
    /// If false, PartialBlockChance is rolled instead.
    /// </summary>
    [DataField]
    public bool FullProtection = true;

    /// <summary>
    /// Chance per exposure tick to block it, when FullProtection is false.
    /// Mirrors DM's prob(80) partial block on BLOCKGASEFFECT gear.
    /// </summary>
    [DataField]
    public float PartialBlockChance = 0.8f;
}