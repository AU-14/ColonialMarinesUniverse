using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Medical;

/// <summary>
/// Adds the life support components carried by <see cref="Grants"/> to this entity
/// (Respirator, Barotrauma, TemperatureDamage, DamageMobState). Dev tool while the
/// species base keeps them disabled. Removing this component removes the whole set,
/// including any of them the entity already had.
/// </summary>
[RegisterComponent]
public sealed partial class CMULifeSupportComponent : Component
{
    /// <summary>
    /// Components already present on the entity are left untouched.
    /// </summary>
    [DataField]
    public EntProtoId Grants = "CMUMobLifeSupport";
}
