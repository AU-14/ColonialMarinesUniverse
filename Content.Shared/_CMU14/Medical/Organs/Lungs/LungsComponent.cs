using System.Collections.Generic;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Organs.Lungs;

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
        { OrganDamageStage.Damaged, FixedPoint2.Zero },
        { OrganDamageStage.Failing, FixedPoint2.New(1)  },
        { OrganDamageStage.Dead,    FixedPoint2.New(5)  },
    };

    [DataField, AutoPausedField]
    public TimeSpan NextAsphyxTick;
}
