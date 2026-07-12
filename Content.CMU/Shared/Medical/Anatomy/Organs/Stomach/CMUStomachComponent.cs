using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Stomach;

/// <summary>
///     CMU-prefixed to avoid clashing with vanilla SS14's <c>StomachComponent</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedStomachSystem))]
public sealed partial class CMUStomachComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DigestionMultiplier = 1.0f;

    [DataField, AutoPausedField]
    public TimeSpan NextVomitCheck;

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
