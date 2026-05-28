using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Stomp;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoStompSystem))]
public sealed partial class XenoStompComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 30;

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(0.4);

    [DataField, AutoNetworkedField]
    public bool ParalyzeUnderOnly = false;

    [DataField, AutoNetworkedField]
    public bool Slows = true;

    [DataField, AutoNetworkedField]
    public TimeSpan SlowTime = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public bool SlowBigInsteadOfStun = false;

    [DataField, AutoNetworkedField]
    public bool DebuffsHurtXenosMore = true;

    [DataField, AutoNetworkedField]
    public float ShortRange = 0.5f;

    [DataField, AutoNetworkedField]
    public float Range = 2.82f;

    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public EntProtoId? SelfEffect;

    // TODO RMC14 bang.ogg
    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_footstep_charge1.ogg");

    /// <summary>
    ///     If true, stomp is a cone aimed at the mouse instead of a circle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Directional = false;

    /// <summary>
    ///     Range (radius) of the directional stomp cone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DirectionalRange = 4f;

    /// <summary>
    ///     Total arc width of the directional stomp cone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle DirectionalAngle = Angle.FromDegrees(90);

    /// <summary>
    ///     Knockback distance for directional stomp.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float KnockBackDistance = 0f;

    /// <summary>
    ///     Screen shake strength for directional stomp.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ScreenShakeStrength = 0;

    /// <summary>
    ///     Effect to spawn on each tile when directional stomp lands.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? DirectionalTileEffect;
}
