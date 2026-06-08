using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Charge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoChargeSystem))]
public sealed partial class XenoChargeComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 20;

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public float Range = 9;

    [DataField, AutoNetworkedField]
    public float SlowRange = 1.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan SlowTime = TimeSpan.FromSeconds(3.5);

    [DataField, AutoNetworkedField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan ChargeDelay = TimeSpan.FromSeconds(0.6);

    [DataField, AutoNetworkedField]
    public SoundSpecifier? ChargeWindupSound;

    // TODO RMC14 extra sound on impact
    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_claw_block.ogg");

    [DataField, AutoNetworkedField]
    public Vector2? Charge;

    [DataField, AutoNetworkedField]
    public float Strength = 30;

    [DataField]
    public HashSet<EntityUid> AlreadyHit = new();

    /// <summary>
    ///     The intended target of the charge. Intermediate mobs get knocked aside.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? PrimaryTarget;

    /// <summary>
    ///     Damage multiplier for intermediate targets knocked aside during charge.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float IntermediateDamageMult = 0.7f;

    /// <summary>
    ///     Where the charge started. Used to calculate distance traveled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2? ChargeOrigin;

    [DataField, AutoNetworkedField]
    public float MinKnockback = 5f;

    [DataField, AutoNetworkedField]
    public float MaxKnockback = 10f;
}
