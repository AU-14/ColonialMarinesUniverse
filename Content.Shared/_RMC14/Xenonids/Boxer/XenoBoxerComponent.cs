using System;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Boxer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoBoxerSystem))]
public sealed partial class XenoBoxerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? KoTarget;

    [DataField, AutoNetworkedField]
    public float KoMeter;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan LastKoHitAt;

    [DataField, AutoNetworkedField]
    public int ClearHeadCharges = XenoBoxerRules.ClearHeadMaxCharges;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextClearHeadRegenAt;

    // Clear Head can intercept several status effects during one action tick. This guard is local
    // state so a single Jab cannot consume two charges.
    public bool ClearHeadConsumedThisTick;

    [DataField]
    public float PunchRange = 2f;

    [DataField]
    public float JabRange = 3f;

    [DataField]
    public float UppercutRange = 1.5f;

    [DataField]
    public float PunchThrowSpeed = 10f;

    [DataField]
    public float PunchKnockBackDistance = 1f;

    [DataField]
    public float PunchSecondKnockBackChance = 0.25f;

    [DataField]
    public TimeSpan PunchCooldown = TimeSpan.FromSeconds(4);

    [DataField]
    public float PunchDamageMin = 20f;

    [DataField]
    public float PunchDamageMax = 25f;

    [DataField]
    public float PunchYautjaDamageMin = 25f;

    [DataField]
    public float PunchYautjaDamageMax = 30f;

    [DataField]
    public float PunchSynthDamageMin = 30f;

    [DataField]
    public float PunchSynthDamageMax = 35f;

    [DataField]
    public float JabSlowMultiplier = 0.5f;

    [DataField]
    public TimeSpan JabDazeDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan JabSlowDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan JabCooldown = TimeSpan.FromSeconds(4);

    [DataField]
    public float UppercutDamagePerKo = 15f;

    [DataField]
    public float UppercutKnockBackPowerPerKo = 1f;

    [DataField]
    public float UppercutKnockBackSpeed = 8f;

    [DataField]
    public TimeSpan UppercutCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan UppercutKnockOutDuration = TimeSpan.FromSeconds(11);

    [DataField]
    public float UppercutHealPercentPerKo = 0.05f;

    [DataField]
    public float XenoVsXenoHealMultiplier = 0.35f;
}
