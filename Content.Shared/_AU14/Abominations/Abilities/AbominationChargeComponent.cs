using Content.Shared.Actions;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._AU14.Abominations.Abilities;

/// <summary>
/// Crusher-style charge ability. Bigger / longer than AbominationLeap and
/// also damages structures it ploughs into. Used by the grunt.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationChargeComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 14f;

    [DataField, AutoNetworkedField]
    public float Strength = 40f;

    [DataField, AutoNetworkedField]
    public TimeSpan FlightDuration = TimeSpan.FromSeconds(1.4);

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public DamageSpecifier MobDamage = new();

    [DataField, AutoNetworkedField]
    public DamageSpecifier StructureDamage = new();

    [DataField, AutoNetworkedField]
    public SoundSpecifier? ChargeSound;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationChargingComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndsAt;

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime;

    [DataField, AutoNetworkedField]
    public DamageSpecifier MobDamage = new();

    [DataField, AutoNetworkedField]
    public DamageSpecifier StructureDamage = new();
}

public sealed partial class AbominationChargeActionEvent : WorldTargetActionEvent;
