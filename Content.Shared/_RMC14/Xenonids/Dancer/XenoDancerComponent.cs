using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Dancer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoDancerSystem))]
public sealed partial class XenoDancerReworkComponent : Component
{
    [DataField, AutoNetworkedField]
    public int PassiveProjectileDodgeEvery = 6;

    [DataField, AutoNetworkedField]
    public int ActiveProjectileDodgeEvery = 3;

    [DataField, AutoNetworkedField]
    public int ProjectileHitsSeen;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextYellowSpreadAt;

    [DataField, AutoNetworkedField]
    public TimeSpan YellowDuration = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public TimeSpan YellowSpreadCooldown = TimeSpan.FromSeconds(20);

    [DataField, AutoNetworkedField]
    public float YellowSpreadRange = 5f;

    [DataField, AutoNetworkedField]
    public int YellowSpreadMaxTargets = 5;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(XenoDancerSystem))]
public sealed partial class XenoYellowMarkedComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan ExpiresAt;
}
