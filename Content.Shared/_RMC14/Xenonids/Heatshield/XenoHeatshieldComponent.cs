using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Heatshield;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoHeatshieldSystem))]
public sealed partial class XenoHeatshieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public float FireDamageMultiplier = 0.5f;

    [DataField, AutoNetworkedField]
    public DamageSpecifier BurningMeleeDamage = new()
    {
        DamageDict = { ["Heat"] = 3 },
    };
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoHeatshieldSystem))]
public sealed partial class XenoThermoregulatingComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.35f;

    [DataField, AutoNetworkedField]
    public float AttackRateMultiplier = 1.2f;
}
