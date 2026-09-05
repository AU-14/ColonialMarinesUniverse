using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Stomach;

/// <summary>
///     CMU-prefixed to avoid clashing with vanilla SS14's <c>StomachComponent</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedStomachSystem))]
public sealed partial class CMUStomachComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DigestionMultiplier = 1.0f;

    [DataField]
    public TimeSpan NextVomitCheck;

    public uint PhysiologyRevision;
    [DataField] public TimeSpan LastPhysiologyUpdate;
    [DataField] public TimeSpan ActiveCheckElapsed;
    [DataField] public OrganDamageStage PhysiologyStage;
    [DataField] public bool PhysiologyActive;

    [DataField]
    public TimeSpan VomitCheckInterval = TimeSpan.FromSeconds(10);

    [DataField]
    public Dictionary<OrganDamageStage, float> VomitChance = new()
    {
        { OrganDamageStage.Healthy, 0f    },
        { OrganDamageStage.Bruised, 0f    },
        { OrganDamageStage.Damaged, 0.03f },
        { OrganDamageStage.Failing, 0.08f },
        { OrganDamageStage.Dead,    0.15f },
    };
}

/// <summary>
///     Tracks that a body had its stomach removed. Without the vanilla stomach
///     organ the body already cannot consume or absorb food, drink, or oral
///     medicine; this marker keeps the associated nausea persistent until a
///     stomach is reinserted.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedStomachSystem))]
public sealed partial class MissingStomachComponent : Component;
