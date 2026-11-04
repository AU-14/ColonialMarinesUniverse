using Content.Shared.Explosion;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Vehicle;

/// <summary>
///     FLOW-09: how hard each kind of explosion hits a tank. A tank's hull is the sum of its modules and blasts are
///     split over them with small module multipliers, so a plain explosion barely scratches it; this scales the blast
///     per explosion type before armour, and <see cref="MaxBlastHullFraction"/> caps what a single blast can remove.
///     The exploding entity's own <see cref="VehicleDamageMultiplierComponent"/> is ignored for blasts on these
///     vehicles, so this profile alone decides blast damage.
/// </summary>
[RegisterComponent]
public sealed partial class CMUTankBlastProfileComponent : Component
{
    /// <summary>
    ///     Blast damage multiplier per explosion type. The defaults are the tank balance (measured by
    ///     CMUTankExplosiveDamageTest): grenades a few percent of a loaded hull, heavy charges and HE rockets up to
    ///     about a fifth, CAS munitions about a fifth to a quarter. Tank prototypes share them by leaving this unset.
    ///     Setting this in YAML replaces the whole dictionary, so every type left out falls back to
    ///     <see cref="DefaultMultiplier"/>; to change a few types, set <see cref="ExtraMultipliers"/> instead.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ExplosionPrototype>, float> Multipliers = new()
    {
        ["RMC"] = 2.5f,
        ["RMCMortar"] = 1.5f,
        ["CMUMortarHEATMP"] = 0.17f,
        ["RMCLTBCannon"] = 0.5f,
        ["CMUBangalore"] = 0.15f,
        ["CMUATGM"] = 1f,
        ["RMCOB"] = 1f,
        ["RMCOBXenoTunnel"] = 1f,
        ["CMUAntiVehicleHE"] = 5f,
        ["CMUHEAP"] = 6f,
        ["CMUIED"] = 7f,
        ["CMUCASMunition"] = 8.5f,
        ["CMUPlasticExplosive"] = 10f,
    };

    /// <summary>
    ///     Per-type overrides checked before <see cref="Multipliers"/>, so a tank prototype can retune a few types
    ///     without restating (and silently dropping) the shared defaults.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ExplosionPrototype>, float> ExtraMultipliers = new();

    /// <summary>
    ///     Multiplier for every explosion type in neither <see cref="ExtraMultipliers"/> nor
    ///     <see cref="Multipliers"/>: for example Default, DemolitionCharge, Minibomb, HardBomb, Cryo, Radioactive and
    ///     CMUYautjaBadBloodPlasma, and any explosion type added later without a tank multiplier of its own.
    /// </summary>
    [DataField]
    public float DefaultMultiplier = 2.5f;

    /// <summary>
    ///     Most of the hull's maximum integrity one blast can remove. Explosion types the tank's
    ///     <see cref="TankCookOffComponent.CatastrophicExplosions"/> lists (orbital bombardment) are not capped.
    /// </summary>
    [DataField]
    public float MaxBlastHullFraction = 0.3f;

    /// <summary>
    ///     The explosion type currently being applied to this tank. The explosion system asks for resistance right
    ///     before it deals the damage, and the damage event does not carry the type.
    /// </summary>
    [ViewVariables]
    public ProtoId<ExplosionPrototype>? PendingExplosion;
}
