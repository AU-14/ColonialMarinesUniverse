using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Xenomorphs.Regen;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUXenoRegenPoolComponent : Component
{
    [DataField, AutoNetworkedField]
    public int MaxCharges = 3;

    [DataField, AutoNetworkedField]
    public float HealthThreshold = 0.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan GracePeriod = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public TimeSpan RechargeTime = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public float RechargeRange = 6f;

    [DataField, AutoNetworkedField]
    public float DepletedWeedsHealingMultiplier = 0.75f;

    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> Alert = "CMUXenoRegenPool";

    [DataField, AutoNetworkedField]
    public int Charges;

    [DataField, AutoNetworkedField]
    public bool BelowThreshold;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextDropAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? RechargeAt;
}

[ByRefEvent]
public record struct CMUXenoPassiveRegenAttemptEvent(bool OnFriendlyWeeds, float Multiplier = 1f);

[ByRefEvent]
public record struct CMUXenoHealerHealEvent;
