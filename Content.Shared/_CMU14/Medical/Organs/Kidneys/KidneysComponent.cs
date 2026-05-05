using System.Collections.Generic;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Organs.Kidneys;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedKidneysSystem))]
public sealed partial class KidneysComponent : Component
{
    [DataField, AutoNetworkedField]
    public float WasteFiltration = 1.0f;

    [DataField]
    public bool IsLeftKidney = true;

    [DataField]
    public Dictionary<OrganDamageStage, FixedPoint2> ToxinPerSecond = new()
    {
        { OrganDamageStage.Healthy, FixedPoint2.Zero     },
        { OrganDamageStage.Bruised, FixedPoint2.Zero     },
        { OrganDamageStage.Damaged, FixedPoint2.Zero     },
        { OrganDamageStage.Failing, FixedPoint2.New(0.25)},
        { OrganDamageStage.Dead,    FixedPoint2.New(0.75)},
    };

    [DataField, AutoPausedField]
    public TimeSpan NextSelfDamageTick;
}
