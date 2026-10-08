using Content.Shared.DoAfter;
using Content.Shared.Damage.Prototypes;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Weapons.Grenades;

/// <summary>
/// G2 Electroshock grenade ("SEBB"), ported from cmss13-devs/cmss13#6667.
/// On trigger it releases an electric pulse: burn and stamina damage to humans (worse for synthetics),
/// burn damage and slowdown to xenonids, falling off with distance. Planting it at your own feet turns it
/// into a buried landmine.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUElectroshockComponent : Component
{
    /// <summary>Maximum range of the pulse, in tiles.</summary>
    [DataField, AutoNetworkedField]
    public float Range = 5;

    /// <summary>Damage dealt at the centre, before falloff.</summary>
    [DataField, AutoNetworkedField]
    public float Damage = 110;

    /// <summary>Damage lost per tile of distance.</summary>
    [DataField, AutoNetworkedField]
    public float FalloffPerTile = 20;

    /// <summary>Applied damage is multiplied by this to get human stamina damage.</summary>
    [DataField, AutoNetworkedField]
    public float HumanStaminaFactor = 0.9f;

    /// <summary>Synthetics take this much more damage from the overvoltage.</summary>
    [DataField, AutoNetworkedField]
    public float SynthDamageMultiplier = 1.5f;

    /// <summary>How long a synthetic caught in the pulse is knocked out for.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SynthStunTime = TimeSpan.FromSeconds(25);

    /// <summary>Applied damage is divided by this to get xenonid slowdown, in seconds.</summary>
    [DataField, AutoNetworkedField]
    public float XenoSlowDivisor = 11;

    /// <summary>Xenonids closer than this many tiles are also superslowed.</summary>
    [DataField, AutoNetworkedField]
    public float XenoSuperslowRange = 2;

    [DataField, AutoNetworkedField]
    public ProtoId<DamageTypePrototype> DamageType = "Heat";

    [DataField, AutoNetworkedField]
    public SoundSpecifier? ExplodeSound = new SoundPathSpecifier("/Audio/CMU14/Weapons/Grenades/sebb_explode.ogg",
        AudioParams.Default.WithVolume(4).WithMaxDistance(16));

    /// <summary>Visual pulse spawned at the detonation point.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? Effect = "CMUElectroshockPulseEffect";

    /// <summary>Planted landmine version. Null if this entity can't be planted.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? MinePrototype;

    [DataField, AutoNetworkedField]
    public TimeSpan PlantDelay = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public SoundSpecifier? PlantSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    /// <summary>
    /// If set, triggering spawns this primed grenade instead of pulsing straight away.
    /// Used by the planted mine, which pops a live grenade out of the ground like in cmss13.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? PrimedPrototype;

    /// <summary>If set, disarming the planted mine gives back this grenade instead of the mine.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? DisarmedPrototype;

    /// <summary>
    /// IFF factions of whoever planted it. The pulse leaves members of these factions alone,
    /// and the grenade popped out of a triggered mine inherits them.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntProtoId<IFFFactionComponent>> Factions = new();

    /// <summary>Trigger keys that set off the pulse. Null keys always do.</summary>
    [DataField]
    public HashSet<string> TriggerKeys = new() { "trigger" };
}

[Serializable, NetSerializable]
public sealed partial class CMUElectroshockPlantDoAfterEvent : SimpleDoAfterEvent;
