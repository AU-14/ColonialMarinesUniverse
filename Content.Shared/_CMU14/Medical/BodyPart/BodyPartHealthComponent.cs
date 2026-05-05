using System.Collections.Generic;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.BodyPart;

/// <summary>
///     Per-body-part HP ledger that runs alongside the entity-level
///     <see cref="Content.Shared.Damage.DamageableComponent"/>. The mob-state machine still
///     reads entity-level damage; this is a side ledger only.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedBodyPartHealthSystem))]
public sealed partial class BodyPartHealthComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 Max = 100;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Current = 100;

    /// <summary>
    ///     Damage past <see cref="Max"/> at which the part severs. Set to a sentinel
    ///     such as <c>999999</c> for parts that must not sever (torso, head guarded
    ///     by safety CCVars).
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 SeveranceThreshold = 200;

    /// <summary>
    ///     Per-damage-group multiplier applied before deducting from <see cref="Current"/>.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<DamageGroupPrototype>, float> Resistance = new();

    [DataField, AutoNetworkedField]
    public bool BoneShieldsOrgans = true;

    [DataField, AutoNetworkedField]
    public bool BlockedByOpenWound = true;

    /// <summary>
    ///     Multiplier on the body-owner's passive heal rate. <c>0</c> = no native part
    ///     regeneration. Applied per <see cref="HealInterval"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PassiveHealMultiplier = 0f;

    [DataField, AutoNetworkedField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(5);

    [DataField, AutoPausedField]
    public TimeSpan NextHealTick;
}
