using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CMU14.Ape;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ApeLeapComponent : Component
{
    // No plasma cost for ape
    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public FixedPoint2 Range = FixedPoint2.New(6);

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public SoundSpecifier? LeapSound;

    [DataField, AutoNetworkedField]
    public int Strength = 20;

    [DataField, AutoNetworkedField]
    public bool KnockdownRequiresInvisibility;

    [DataField, AutoNetworkedField]
    public TimeSpan MoveDelayTime = TimeSpan.FromSeconds(.7);

    [DataField, AutoNetworkedField]
    public bool UnrootOnMelee;

    [DataField, AutoNetworkedField]
    public bool DestroyObjects;

    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public EntProtoId? HitEffect;

    [DataField, AutoNetworkedField]
    public TimeSpan TargetJitterTime = TimeSpan.FromSeconds(0);

    [DataField, AutoNetworkedField]
    public int TargetCameraShakeStrength;

    [DataField, AutoNetworkedField]
    public CollisionGroup IgnoredCollisionGroupLarge = CollisionGroup.BarricadeImpassable | CollisionGroup.MidImpassable;

    [DataField, AutoNetworkedField]
    public CollisionGroup IgnoredCollisionGroupSmall = CollisionGroup.BarricadeImpassable;

    [DataField, AutoNetworkedField]
    public EntityUid? LastHit;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? LastHitAt;

    [DataField, AutoNetworkedField]
    public float LastHitRange = 10;
}

