using System.Collections.Generic;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedLiverSystem))]
public sealed partial class LiverComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ToxinClearMultiplier = 1.0f;

    [DataField]
    public Dictionary<OrganDamageStage, FixedPoint2> ToxinPerSecond = new()
    {
        { OrganDamageStage.Healthy, FixedPoint2.Zero },
        { OrganDamageStage.Bruised, FixedPoint2.New(0.05) },
        { OrganDamageStage.Damaged, FixedPoint2.New(0.15) },
        { OrganDamageStage.Failing, FixedPoint2.New(0.5) },
        { OrganDamageStage.Dead, FixedPoint2.New(1) },
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
[Access(typeof(SharedLiverSystem))]
public sealed partial class MissingLiverComponent : Component
{
    [DataField]
    public TimeSpan NextSelfDamageTick;

    public uint PhysiologyRevision;
    [DataField] public TimeSpan LastPhysiologyUpdate;
    [DataField] public bool PhysiologyActive;
    [DataField] public double ToxinRemainder;
}
