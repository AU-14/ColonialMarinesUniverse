using Content.Shared.Damage;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CMU14.Threats.Mobs.Ape;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause,
 Access(typeof(ApeLeapSystem))]
public sealed partial class ApeLeapingComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public bool DestroyObjects;

    [DataField, AutoNetworkedField]
    public EntProtoId? HitEffect;

    [DataField, AutoNetworkedField]
    public CollisionGroup IgnoredCollisionGroupLarge;

    [DataField, AutoNetworkedField]
    public CollisionGroup IgnoredCollisionGroupSmall;

    [DataField, AutoNetworkedField]
    public bool KnockdownRequiresInvisibility;

    [DataField, AutoNetworkedField]
    public bool KnockedDown;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LeapEndTime;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? LeapSound;

    [DataField, AutoNetworkedField]
    public TimeSpan MoveDelayTime;

    [DataField, AutoNetworkedField]
    public EntityCoordinates Origin;

    [DataField, AutoNetworkedField]
    public TimeSpan ParalyzeTime;

    [DataField, AutoNetworkedField]
    public bool PlayedSound;

    [DataField, AutoNetworkedField]
    public int TargetCameraShakeStrength;

    [DataField, AutoNetworkedField]
    public TimeSpan TargetJitterTime;
}
