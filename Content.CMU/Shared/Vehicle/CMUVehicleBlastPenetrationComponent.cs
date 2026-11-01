using Content.Shared.Explosion;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Vehicle;

/// <summary>
///     Replicates an explosion that lands on this vehicle inside its interior map as armour "penetration".
///     What gets through depends on what caused the blast: the defaults are the armoured profile shared by
///     humvees, APCs, tanks and the Blackfoot, where only heavy ordnance (rockets, tank shells, IEDs, CAS,
///     breaching charges, orbital fire) breaches the cabin and grenades do not; soft-skinned vehicles such as
///     vans and civilian trucks set <see cref="DefaultIntensity"/> so that everything, grenades included,
///     blasts through into the interior.
/// </summary>
[RegisterComponent]
public sealed partial class CMUVehicleBlastPenetrationComponent : Component
{
    /// <summary>
    ///     Total intensity of the interior blast per exterior explosion type; 0 means the type cannot breach the
    ///     cabin. Setting this in YAML replaces the whole dictionary and every type left out falls back to
    ///     <see cref="DefaultIntensity"/>; to change a few types, set <see cref="ExtraIntensities"/> instead.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ExplosionPrototype>, float> Intensities = new()
    {
        ["RMCLTBCannon"] = 20f,
        ["RMCMortar"] = 12f,
        ["RMCOB"] = 60f,
        ["RMCOBXenoTunnel"] = 60f,
        ["CMUATGM"] = 25f,
        ["CMUAntiVehicleHE"] = 20f,
        ["CMUHEAP"] = 25f,
        ["CMUIED"] = 25f,
        ["CMUCASMunition"] = 30f,
        ["CMUPlasticExplosive"] = 30f,
        ["CMUMortarHEATMP"] = 18f,
    };

    /// <summary>
    ///     Per-type overrides checked before <see cref="Intensities"/>, so a vehicle prototype can retune a few
    ///     types without restating (and silently dropping) the shared defaults.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ExplosionPrototype>, float> ExtraIntensities = new();

    /// <summary>
    ///     Interior blast intensity for every explosion type in neither <see cref="ExtraIntensities"/> nor
    ///     <see cref="Intensities"/>. 0 (the armoured default) blocks grenades and any other unlisted type;
    ///     soft-skinned vehicles raise this so that everything penetrates.
    /// </summary>
    [DataField]
    public float DefaultIntensity;

    /// <summary>Intensity falloff slope of the replicated interior blast.</summary>
    [DataField]
    public float Slope = 5f;

    /// <summary>Per-tile intensity cap of the replicated interior blast.</summary>
    [DataField]
    public float MaxTileIntensity = 6f;

    /// <summary>
    ///     How far outside the hull's world bounds the exterior epicenter may land and still count as a
    ///     penetrating hit; blasts that merely clip the footprint from further away stay outside.
    /// </summary>
    [DataField]
    public float RangeBuffer = 1.5f;
}
