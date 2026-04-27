using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Ordnance;

/// <summary>
///     UI channel for the explosion simulator computer.
/// </summary>
[Serializable, NetSerializable]
public enum RMCExplosionSimulatorUiKey
{
    Key,
}

/// <summary>
///     Requests a fresh two-minute simulation pass for the inserted ordnance sample.
/// </summary>
[Serializable, NetSerializable]
public sealed class RMCExplosionSimulatorSimulateMsg : BoundUserInterfaceMessage;

/// <summary>
///     Switches the replay formation that the console will spawn inside its test chamber.
/// </summary>
[Serializable, NetSerializable]
public sealed class RMCExplosionSimulatorTargetMsg(RMCExplosionSimulatorTarget target) : BoundUserInterfaceMessage
{
    public readonly RMCExplosionSimulatorTarget Target = target;
}

/// <summary>
///     Requests the chamber replay for the most recently completed analysis package.
/// </summary>
[Serializable, NetSerializable]
public sealed class RMCExplosionSimulatorReplayMsg : BoundUserInterfaceMessage;

/// <summary>
///     Snapshot of the console state used by the client to render analysis progress and results.
/// </summary>
[Serializable, NetSerializable]
public sealed class RMCExplosionSimulatorBuiState(
    bool hasSample,
    string sampleName,
    RMCExplosionSimulatorTarget target,
    bool isProcessing,
    int processingSecsLeft,
    bool simulationReady,
    bool hasExplosion,
    float totalIntensity,
    float intensitySlope,
    float maxIntensity,
    bool hasFire,
    float fireIntensity,
    float fireRadius,
    float fireDuration,
    NetEntity? cameraNetId) : BoundUserInterfaceState
{
    public readonly bool HasSample = hasSample;
    public readonly string SampleName = sampleName;
    public readonly RMCExplosionSimulatorTarget Target = target;
    public readonly bool IsProcessing = isProcessing;
    public readonly int ProcessingSecsLeft = processingSecsLeft;
    public readonly bool SimulationReady = simulationReady;
    public readonly bool HasExplosion = hasExplosion;
    public readonly float TotalIntensity = totalIntensity;
    public readonly float IntensitySlope = intensitySlope;
    public readonly float MaxIntensity = maxIntensity;
    public readonly bool HasFire = hasFire;
    public readonly float FireIntensity = fireIntensity;
    public readonly float FireRadius = fireRadius;
    public readonly float FireDuration = fireDuration;
    public readonly NetEntity? CameraNetId = cameraNetId;
}
