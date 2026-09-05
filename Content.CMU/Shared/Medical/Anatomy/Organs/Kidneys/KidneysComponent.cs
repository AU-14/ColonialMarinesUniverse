using System.Collections.Generic;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Kidneys;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
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

    // Global time is settled explicitly at every active/frozen boundary.
    [DataField]
    public TimeSpan NextSelfDamageTick;

    public uint PhysiologyRevision;
    [DataField] public TimeSpan LastPhysiologyUpdate;
    [DataField] public OrganDamageStage PhysiologyStage;
    [DataField] public bool PhysiologyActive;
    [DataField] public double ToxinRemainder;
}

[RegisterComponent]
[Access(typeof(SharedKidneysSystem))]
public sealed partial class MissingKidneysComponent : Component
{
    [DataField]
    public TimeSpan NextSelfDamageTick;

    public uint PhysiologyRevision;
    [DataField] public TimeSpan LastPhysiologyUpdate;
    [DataField] public bool PhysiologyActive;
    [DataField] public double ToxinRemainder;
}
