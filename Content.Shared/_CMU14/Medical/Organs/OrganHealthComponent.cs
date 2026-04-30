using System.Collections.Generic;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Organs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedOrganHealthSystem))]
public sealed partial class OrganHealthComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 Max = 50;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Current = 50;

    /// <summary>
    ///     Per-damage-group multiplier applied to incoming damage. An organ
    ///     omitting an entry takes zero damage of that group from the
    ///     part-distribution pipeline.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<DamageGroupPrototype>, float> DamageWeight = new();

    /// <summary>
    ///     Stage floors. Picked descending — the lowest threshold whose value
    ///     is &gt;= <see cref="Current"/> wins.
    /// </summary>
    [DataField]
    public Dictionary<OrganDamageStage, FixedPoint2> StageThresholds = new()
    {
        { OrganDamageStage.Healthy, 50 },
        { OrganDamageStage.Bruised, 35 },
        { OrganDamageStage.Damaged, 20 },
        { OrganDamageStage.Failing, 5 },
        { OrganDamageStage.Dead,    0 },
    };

    [DataField, AutoNetworkedField]
    public OrganDamageStage Stage = OrganDamageStage.Healthy;

    [DataField]
    public OrganDamageStage InternalBleedAt = OrganDamageStage.Damaged;

    /// <summary>
    ///     HP regenerated per 10s tick while the organ is in Healthy / Bruised.
    ///     0 = never naturally regenerates.
    /// </summary>
    [DataField]
    public FixedPoint2 NativeRegenPerTick = 1;

    /// <summary>
    ///     Hard cap as a fraction of <see cref="Max"/> for native regen — forces
    ///     players to seek medical care for full recovery.
    /// </summary>
    [DataField]
    public float NativeRegenCap = 0.9f;

    [DataField, AutoPausedField]
    public TimeSpan NextRegenTick;
}
