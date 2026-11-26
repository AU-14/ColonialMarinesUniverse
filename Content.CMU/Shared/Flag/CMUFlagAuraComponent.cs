using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Flag;

/// <summary>
///     Lets a flag rally nearby allies with marine orders while it is carried, planted, or mounted on a vehicle.
///     Orders are applied by the server.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUFlagAuraSystem))]
public sealed partial class CMUFlagAuraComponent : Component
{
    /// <summary>
    ///     Aura while the flag is held in a hand. Null disables it.
    /// </summary>
    [DataField]
    public CMUFlagAuraStats? Carried;

    /// <summary>
    ///     Aura while the flag is planted (anchored). Null disables it.
    /// </summary>
    [DataField]
    public CMUFlagAuraStats? Planted;

    /// <summary>
    ///     Aura while the flag is installed in a vehicle hardpoint slot. Null disables it.
    /// </summary>
    [DataField]
    public CMUFlagAuraStats? Mounted;

    /// <summary>
    ///     Whether a mounted flag comes down while its vehicle is at <see cref="LowerAtIntegrity"/>.
    ///     Otherwise it flies whenever it's mounted, driver or not.
    /// </summary>
    [DataField]
    public bool RequiresRaising;

    [DataField]
    public bool Raised;

    /// <summary>
    ///     Share of its vehicle's integrity at or below which the flag comes down until the vehicle is repaired.
    /// </summary>
    [DataField]
    public float LowerAtIntegrity = 0.35f;

    /// <summary>
    ///     IFF factions of the last bearer. A planted flag keeps rallying the side that planted it.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId<IFFFactionComponent>> Factions = new();

    /// <summary>
    ///     The round time at which the flag will next check for allies.
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan NextOrder;
}

/// <summary>
///     The orders a flag grants and how far they reach.
/// </summary>
[DataDefinition]
public sealed partial class CMUFlagAuraStats
{
    /// <summary>
    ///     The range in which the orders are applied.
    /// </summary>
    [DataField]
    public float Range = 4.515f;

    /// <summary>
    ///     The strength of the applied orders.
    /// </summary>
    [DataField]
    public int Strength = 4;

    [DataField]
    public bool ApplyFocus = true;

    [DataField]
    public bool ApplyHold = true;

    [DataField]
    public bool ApplyMove;

    /// <summary>
    ///     How long each application of the orders lasts.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1.5);

    /// <summary>
    ///     How long it takes before the flag applies its orders again.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1.5);
}
