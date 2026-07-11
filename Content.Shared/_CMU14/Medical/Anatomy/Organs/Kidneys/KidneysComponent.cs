using System.Collections.Generic;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Kidneys;

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
        { OrganDamageStage.Healthy, FixedPoint2.Zero },
        { OrganDamageStage.Bruised, FixedPoint2.New(0.05) },
        { OrganDamageStage.Damaged, FixedPoint2.New(0.15) },
        { OrganDamageStage.Failing, FixedPoint2.New(0.25) },
        { OrganDamageStage.Dead, FixedPoint2.New(0.75) },
    };

    [DataField, AutoPausedField]
    public TimeSpan NextSelfDamageTick;
}

[RegisterComponent]
[Access(typeof(SharedKidneysSystem))]
public sealed partial class MissingKidneysComponent : Component
{
    [DataField]
    public TimeSpan NextSelfDamageTick;
}
