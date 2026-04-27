using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Ordnance;

/// <summary>
///     Server-owned state for the explosion simulator computer.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCExplosionSimulatorComponent : Component
{
    /// <summary>
    ///     Console analysis time. The UI counts this down live while the server tracks the authoritative end time.
    /// </summary>
    public static readonly TimeSpan ProcessingDuration = TimeSpan.FromSeconds(120);

    /// <summary>
    ///     Current replay formation selected in the UI.
    /// </summary>
    [DataField, AutoNetworkedField]
    public RMCExplosionSimulatorTarget SelectedTarget = RMCExplosionSimulatorTarget.Marines;

    /// <summary>
    ///     True while the console is processing the inserted ordnance sample.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsProcessing;

    /// <summary>
    ///     Absolute end time for the active analysis cycle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ProcessingEnd;

    /// <summary>
    ///     Set once the last completed simulation can be replayed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SimulationReady;

    /// <summary>
    ///     Net ID of the replay chamber camera so clients can subscribe to it when needed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity? ChamberCamera;

    /// <summary>
     ///     Cached explosive estimate from the most recent simulation pass.
     /// </summary>
    [DataField, AutoNetworkedField]
    public bool LastHasExplosion;

    [DataField, AutoNetworkedField]
    public float LastTotalIntensity;

    [DataField, AutoNetworkedField]
    public float LastIntensitySlope;

    [DataField, AutoNetworkedField]
    public float LastMaxIntensity;

    [DataField, AutoNetworkedField]
    public bool LastHasFire;

    [DataField, AutoNetworkedField]
    public float LastFireIntensity;

    [DataField, AutoNetworkedField]
    public float LastFireRadius;

    [DataField, AutoNetworkedField]
    public float LastFireDuration;

    /// <summary>
    ///     Chamber grid loaded for replay instead of being generated procedurally.
    /// </summary>
    [DataField("chamberMap")]
    public ResPath ChamberMap = RMCOrdnanceSimulationDefaults.ChamberMap;

    /// <summary>
    ///     Server-only replay chamber map for the currently cached result.
    /// </summary>
    public EntityUid ChamberMapEnt = EntityUid.Invalid;

    /// <summary>
    ///     Server-side entity for the current chamber camera.
    /// </summary>
    public EntityUid ChamberCameraEnt = EntityUid.Invalid;

    /// <summary>
    ///     Actor who started the current analysis cycle and should receive completion feedback.
    /// </summary>
    public EntityUid ProcessingActor = EntityUid.Invalid;

    /// <summary>
    ///     Whether a replay explosion is queued for the chamber camera position.
    /// </summary>
    public bool PendingExplosion;
    public TimeSpan ExplosionAt;
    public float PendingTotalIntensity;
    public float PendingIntensitySlope;
    public float PendingMaxIntensity;
    public bool PendingFire;
    public float PendingFireIntensity;
    public float PendingFireRadius;
    public float PendingFireDuration;
    public EntProtoId PendingFireEntity = "RMCTileFire";
}

/// <summary>
///     Supported replay formations for the explosion simulator chamber.
/// </summary>
public enum RMCExplosionSimulatorTarget : byte
{
    Marines = 0,
    SpecialForces = 1,
    Xenomorphs = 2,
}
