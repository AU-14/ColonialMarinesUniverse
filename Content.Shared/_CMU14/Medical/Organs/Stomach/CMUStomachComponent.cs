using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Organs.Stomach;

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
