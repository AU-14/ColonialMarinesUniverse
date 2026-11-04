using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Medical.RecoveryPosition;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class CMURecoveryPositionComponent : Component
{
    [DataField]
    public Vector2 Position;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextHeal;

    [DataField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(6);

    [DataField]
    public FixedPoint2 HealAmount = FixedPoint2.New(1);

    [DataField]
    public float MoveTolerance = 0.2f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextRotBonus;

    [DataField]
    public TimeSpan RotBonusInterval = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan RotBonus = TimeSpan.FromSeconds(6);
}

[Serializable, NetSerializable]
public sealed partial class CMURecoveryPositionDoAfterEvent : SimpleDoAfterEvent;
