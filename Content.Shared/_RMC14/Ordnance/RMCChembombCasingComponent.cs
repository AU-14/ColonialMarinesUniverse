using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Ordnance;

/// <summary>
///     Assembly stages for a custom ordnance casing.
/// </summary>
[Serializable, NetSerializable]
public enum RMCCasingAssemblyStage : byte
{
    /// <summary>
    ///     Casing is open and can still be filled or accept a detonator assembly.
    /// </summary>
    Open,

    /// <summary>
    ///     Casing is closed but not yet live.
    /// </summary>
    Sealed,

    /// <summary>
    ///     Casing is fully armed and will react to its trigger path.
    /// </summary>
    Armed,
}

/// <summary>
///     Shared configuration and replicated state for grenade, mine, rocket, mortar, and C4-style chemical casings.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCChembombCasingComponent : Component
{
    /// <summary>Maximum chemical volume this casing can hold.</summary>
    [DataField(required: true), AutoNetworkedField]
    public FixedPoint2 MaxVolume;

    /// <summary>Base explosion power before chemical modifiers are applied.</summary>
    [DataField, AutoNetworkedField]
    public float BasePower = 180f;

    /// <summary>Base explosion falloff before chemical modifiers are applied.</summary>
    [DataField, AutoNetworkedField]
    public float BaseFalloff = 80f;

    /// <summary>
    ///     Maximum number of shrapnel projectiles this casing may emit.
    ///     CMSS13 treats the listed shard value as a cap, not as the default runtime count.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxShards;

    /// <summary>Whether a valid detonator assembly is currently installed.</summary>
    [DataField, AutoNetworkedField]
    public bool HasActiveDetonator;

    /// <summary>Current assembly state of the casing.</summary>
    [DataField, AutoNetworkedField]
    public RMCCasingAssemblyStage Stage;

    /// <summary>Name of the solution container holding the chemical payload.</summary>
    [DataField]
    public string ChemicalSolution = "chemicals";

    /// <summary>Minimum fire intensity after reagent modifiers are applied.</summary>
    [DataField]
    public float MinFireIntensity = 3f;

    /// <summary>Maximum fire intensity after reagent modifiers are applied.</summary>
    [DataField]
    public float MaxFireIntensity = 25f;

    /// <summary>Minimum fire radius after reagent modifiers are applied.</summary>
    [DataField]
    public float MinFireRadius = 1f;

    /// <summary>Maximum fire radius after reagent modifiers are applied.</summary>
    [DataField]
    public float MaxFireRadius = 5f;

    /// <summary>Minimum fire duration after reagent modifiers are applied.</summary>
    [DataField]
    public float MinFireDuration = 3f;

    /// <summary>Maximum fire duration after reagent modifiers are applied.</summary>
    [DataField]
    public float MaxFireDuration = 24f;

    /// <summary>Fallback fire entity when no reagent overrides the spawned fire type.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId DefaultFireEntity = "RMCTileFire";

    /// <summary>Minimum effective falloff used by the custom ordnance parity math.</summary>
    [DataField]
    public float MinFalloff = 25f;

    /// <summary>Default shrapnel projectile used when no special shard type is present.</summary>
    [DataField]
    public EntProtoId DefaultShrapnelProto = "CMProjectileShrapnel";

    /// <summary>Projectile used when the payload crosses the incendiary shard threshold.</summary>
    [DataField]
    public EntProtoId IncendiaryShrapnelProto = "RMCShrapnelIncendiary";

    /// <summary>Projectile used when the payload crosses the hornet shard threshold.</summary>
    [DataField]
    public EntProtoId HornetShrapnelProto = "RMCHornetRound";

    /// <summary>
    ///     Projectile used for neuro shards.
    ///     We currently fall back to plain shrapnel until a dedicated local neuro shard projectile is tuned.
    /// </summary>
    [DataField]
    public EntProtoId NeuroShrapnelProto = "CMProjectileShrapnel";

    /// <summary>Angular spread, in degrees, used when shards are emitted.</summary>
    [DataField]
    public float ShrapnelSpread = 360f;

    /// <summary>Whether the shrapnel cone should be oriented around the casing's facing direction.</summary>
    [DataField]
    public bool DirectionalShrapnel;

    /// <summary>Projectile speed for spawned shards.</summary>
    [DataField]
    public float ShrapnelSpeed = 20f;
}

/// <summary>
///     Completes the casing seal or arm action after the associated do-after finishes.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RMCCasingScrewDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
///     Reserved for casing wire or cut interactions.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RMCCasingCutDoAfterEvent : SimpleDoAfterEvent;
