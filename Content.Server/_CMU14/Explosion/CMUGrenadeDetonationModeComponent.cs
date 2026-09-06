using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Explosion;

/// <summary>
/// CMU-owned selectable detonation behavior for compatible grenades.
/// Timed mode leaves the upstream timer path unchanged.
/// Impact mode arms the grenade but removes the active countdown.
/// </summary>
public enum CMUGrenadeDetonationMode : byte
{
    Timed,
    Impact,
}

[RegisterComponent]
[Access(typeof(CMUGrenadeDetonationSystem))]
public sealed partial class CMUGrenadeDetonationModeComponent : Component
{
    /// <summary>
    /// Selected detonation mode. Existing grenades default to their normal timed behavior.
    /// </summary>
    [DataField]
    public CMUGrenadeDetonationMode Mode = CMUGrenadeDetonationMode.Timed;

    /// <summary>
    /// Multiplier applied to payload strength while this grenade is in Impact mode.
    /// The default of 0.75 represents a 25% reduction.
    /// </summary>
    [DataField]
    public float ImpactPayloadMultiplier = 0.75f;

    /// <summary>
    /// Optional reduced payload prototype used instead of SpawnOnTrigger's normal
    /// prototype while this grenade is in Impact mode.
    /// </summary>
    [DataField]
    public EntProtoId? ImpactSpawn;

    /// <summary>
    /// True after an Impact-mode grenade has been primed.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Armed;

    /// <summary>
    /// Best available attribution for the entity that armed/threw/fired the grenade.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ArmedBy;
}
