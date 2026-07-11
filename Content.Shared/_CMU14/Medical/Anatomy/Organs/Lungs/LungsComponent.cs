using System.Collections.Generic;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Lungs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedLungsSystem))]
public sealed partial class LungsComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Efficiency = 1.0f;

    /// <summary>
    ///     Per-stage asphyxiation damage (in Damage units) inflicted on the body
    ///     once per second while this lung sits at the given stage. Zero entries
    ///     mean "no self-damage at this stage".
    /// </summary>
    [DataField]
    public Dictionary<OrganDamageStage, FixedPoint2> AsphyxPerSecond = new()
    {
        { OrganDamageStage.Healthy, FixedPoint2.Zero },
        { OrganDamageStage.Bruised, FixedPoint2.Zero },
        { OrganDamageStage.Damaged, FixedPoint2.New(0.5) },
        { OrganDamageStage.Failing, FixedPoint2.New(2)  },
        { OrganDamageStage.Dead,    FixedPoint2.New(5)  },
    };

    [DataField, AutoPausedField]
    public TimeSpan NextAsphyxTick;

    [DataField]
    public Dictionary<OrganDamageStage, float> BloodCoughChance = new()
    {
        { OrganDamageStage.Healthy, 0f },
        { OrganDamageStage.Bruised, 0.02f },
        { OrganDamageStage.Damaged, 0.10f },
        { OrganDamageStage.Failing, 0.25f },
        { OrganDamageStage.Dead, 0.50f },
    };

    [DataField]
    public TimeSpan BloodCoughInterval = TimeSpan.FromSeconds(10);

    [DataField]
    public FixedPoint2 BloodLossPerCough = FixedPoint2.New(2.5);

    [DataField, AutoPausedField]
    public TimeSpan NextBloodCoughCheck;
}

[RegisterComponent]
[Access(typeof(SharedLungsSystem))]
public sealed partial class MissingLungsComponent : Component
{
    [DataField]
    public TimeSpan NextAsphyxTick;
}
