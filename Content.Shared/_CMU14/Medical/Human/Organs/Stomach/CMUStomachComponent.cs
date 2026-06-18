using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Data;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Human.Organs.Stomach;

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
    public Dictionary<OrganDamageStatus, float> VomitChance = new()
    {
        { OrganDamageStatus.None, 0f },
        { OrganDamageStatus.LittleBruised, 0f },
        { OrganDamageStatus.Bruised, 0.03f },
        { OrganDamageStatus.Broken, 0.08f },
    };
}
