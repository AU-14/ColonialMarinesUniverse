using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Ghost.Roles;

/// <summary>
///     One rail entry in the ghost roles window: a faction, or the catch-all every uncategorised
///     role falls into.
/// </summary>
[Prototype]
public sealed partial class GhostRoleCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>What the rail prints. Expected to be short - it shares a 210px rail with a count.</summary>
    [DataField(required: true)]
    public LocId Name { get; private set; }

    /// <summary>
    ///     The accent this faction's banners, counts and take buttons are drawn in.
    /// </summary>
    [DataField]
    public Color Color { get; private set; } = Color.FromHex("#B366FF");

    /// <summary>Rail order, low first. Ties fall back to the localised name.</summary>
    [DataField]
    public int Weight { get; private set; }
}
