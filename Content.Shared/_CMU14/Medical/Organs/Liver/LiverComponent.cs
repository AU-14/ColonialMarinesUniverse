using System.Collections.Generic;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Organs.Liver;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedLiverSystem))]
public sealed partial class LiverComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ToxinClearMultiplier = 1.0f;

    [DataField]
    public Dictionary<OrganDamageStage, FixedPoint2> ToxinPerSecond = new()
    {
        { OrganDamageStage.Healthy, FixedPoint2.Zero    },
        { OrganDamageStage.Bruised, FixedPoint2.Zero    },
        { OrganDamageStage.Damaged, FixedPoint2.Zero    },
        { OrganDamageStage.Failing, FixedPoint2.New(0.5)},
        { OrganDamageStage.Dead,    FixedPoint2.New(1)  },
    };

    [DataField, AutoPausedField]
    public TimeSpan NextSelfDamageTick;
}
