using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Ordnance;

/// <summary>
///     UI channel for the handheld demolitions scanner.
/// </summary>
[Serializable, NetSerializable]
public enum RMCDemolitionsScannerUiKey
{
    Key,
}

/// <summary>
///     Full readout generated when the scanner inspects a custom casing.
/// </summary>
[Serializable, NetSerializable]
public sealed class RMCDemolitionsScannerBuiState(
    string casingName,
    float currentVolume,
    float maxVolume,
    bool hasDetonator,
    RMCCasingAssemblyStage stage,
    bool hasExplosion,
    float blastPower,
    float blastFalloff,
    float blastRadius,
    bool hasFire,
    float fireIntensity,
    float fireRadius,
    float fireDuration) : BoundUserInterfaceState
{
    public readonly string CasingName = casingName;
    public readonly float CurrentVolume = currentVolume;
    public readonly float MaxVolume = maxVolume;
    public readonly bool HasDetonator = hasDetonator;
    public readonly RMCCasingAssemblyStage Stage = stage;
    public readonly bool HasExplosion = hasExplosion;
    public readonly float BlastPower = blastPower;
    public readonly float BlastFalloff = blastFalloff;
    public readonly float BlastRadius = blastRadius;
    public readonly bool HasFire = hasFire;
    public readonly float FireIntensity = fireIntensity;
    public readonly float FireRadius = fireRadius;
    public readonly float FireDuration = fireDuration;
}
